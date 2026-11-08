package fr.f5pbg.screenlink;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioPlaybackCaptureConfiguration;
import android.media.AudioRecord;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.os.Build;
import android.os.Bundle;
import android.view.Surface;

import java.io.DataInputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.Iterator;
import java.util.concurrent.LinkedBlockingDeque;
import java.util.concurrent.atomic.AtomicInteger;

/** Une connexion d'un PC : authentification, envoi de l'image (H.264) et du son (PCM), réception des commandes. */
final class Session implements Runnable {
    static final int V_CONFIG = 1, V_FRAME = 2, A_CONFIG = 3, A_DATA = 4, INFO = 5;
    static final int C_TOUCH = 10, C_SCROLL = 11, C_KEY = 12, C_TEXT = 13, C_SETTINGS = 14, C_KEYFRAME = 15, C_PING = 16;
    static final int RATE = 48000;

    private final CaptureService svc;
    private final Socket sock;
    private DataInputStream in;
    private OutputStream out;
    private final LinkedBlockingDeque<byte[]> qa = new LinkedBlockingDeque<>();   // son + infos : prioritaire
    private final LinkedBlockingDeque<byte[]> qv = new LinkedBlockingDeque<>();   // image
    private final java.util.concurrent.Semaphore sem = new java.util.concurrent.Semaphore(0);
    private final AtomicInteger videoQ = new AtomicInteger(), audioQ = new AtomicInteger();
    private volatile boolean closed;
    private volatile boolean waitKey = true;
    volatile boolean authenticated;

    // réglages demandés par le PC
    private int maxSize = 1920, fps = 30, kbps = 8000;
    private boolean audioOn = true;
    private boolean started;

    private final Object vLock = new Object();
    private MediaCodec enc;
    private Thread drain;
    private volatile boolean drainStop;
    private int encW, encH;
    private byte[] config;

    private AudioRecord rec;
    private Thread audioThread;
    private volatile boolean audioStop;
    private long lastNoInput;

    Session(CaptureService svc, Socket s) { this.svc = svc; this.sock = s; }

    boolean isClosed() { return closed; }

    void close() {
        if (closed) return;
        closed = true;
        try { sock.close(); } catch (Exception e) { }
        qa.offer(new byte[]{0, 0, 0, 0, 0});     // réveille le fil d écriture
        sem.release();
    }

    // ---------------------------------------------------------------- connexion
    @Override public void run() {
        Thread writer = null;
        try {
            sock.setTcpNoDelay(true);
            sock.setKeepAlive(true);
            try { sock.setSendBufferSize(65536); } catch (Exception e) { }
            sock.setSoTimeout(8000);
            in = new DataInputStream(sock.getInputStream());
            out = sock.getOutputStream();

            byte[] nonce = new byte[16];
            new SecureRandom().nextBytes(nonce);
            out.write(new byte[]{'S', 'L', 'N', 'K', 1});
            out.write(nonce);
            out.flush();
            byte[] got = new byte[32];
            in.readFully(got);
            if (svc.locked()) { out.write(3); out.flush(); return; }
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            md.update(nonce);
            md.update(svc.pin().getBytes("UTF-8"));
            if (!MessageDigest.isEqual(md.digest(), got)) {
                svc.failed();
                Thread.sleep(1500);
                out.write(1); out.flush();
                return;
            }
            if (!svc.attach(this)) { out.write(2); out.flush(); return; }
            authenticated = true;
            svc.ok();
            out.write(0); out.flush();

            writer = new Thread(new Runnable() { public void run() { writeLoop(); } }, "SL-writer");
            writer.setPriority(Thread.MAX_PRIORITY);
            writer.start();
            sendInfo();
            readLoop();
        } catch (Exception e) {
            // connexion perdue
        } finally {
            close();
            stopVideo();
            stopAudio();
            if (writer != null) { try { writer.join(1000); } catch (Exception e) { } }
            svc.setPhoneMute(false);
            svc.detach(this);
        }
    }

    private void sendInfo() {
        String t = "name=" + Build.MANUFACTURER + " " + Build.MODEL + "\n"
                + "input=" + (InputService.inst != null ? 1 : 0) + "\n"
                + "audio=" + (Build.VERSION.SDK_INT >= 29 ? 1 : 0) + "\n";
        put(INFO, t.getBytes());
    }

    private void readLoop() throws IOException {
        while (!closed) {
            int type = in.readUnsignedByte();
            int len = in.readInt();
            if (len < 0 || len > (1 << 20)) throw new IOException("trame invalide");
            byte[] p = new byte[len];
            in.readFully(p);
            handle(type, ByteBuffer.wrap(p));
        }
    }

    private void handle(int type, ByteBuffer b) {
        InputService is = InputService.inst;
        switch (type) {
            case C_PING: break;
            case C_SETTINGS: {
                maxSize = b.getShort() & 0xFFFF;
                fps = b.get() & 0xFF;
                kbps = b.getInt();
                audioOn = b.get() != 0;
                svc.setPhoneMute(b.hasRemaining() && b.get() != 0);
                if (maxSize < 160) maxSize = 160;
                if (fps < 5) fps = 5; if (fps > 60) fps = 60;
                if (kbps < 500) kbps = 500;
                if (!started) {
                    started = true;
                    startVideo();
                    if (audioOn) startAudio();
                } else {
                    startVideo();
                    stopAudio();
                    if (audioOn) startAudio();
                }
                break;
            }
            case C_KEYFRAME: requestKey(); break;
            case C_TOUCH: {
                int a = b.get();
                float x = b.getFloat(), y = b.getFloat();
                if (is == null) { noInput(); break; }
                is.touch(a, x, y);
                break;
            }
            case C_SCROLL: {
                float x = b.getFloat(), y = b.getFloat();
                int dx = b.getShort(), dy = b.getShort();
                boolean ctrl = b.get() != 0;
                if (is == null) { noInput(); break; }
                is.scroll(x, y, dx, dy, ctrl);
                break;
            }
            case C_KEY: {
                int k = b.get() & 0xFF;
                if (is == null) { noInput(); break; }
                is.key(k);
                break;
            }
            case C_TEXT: {
                byte[] t = new byte[b.remaining()];
                b.get(t);
                if (is == null) { noInput(); break; }
                try { is.text(new String(t, "UTF-8")); } catch (Exception e) { }
                break;
            }
        }
    }

    private void noInput() {
        long n = System.currentTimeMillis();
        if (n - lastNoInput > 3000) { lastNoInput = n; sendInfo(); }
    }

    // ---------------------------------------------------------------- envoi
    private void put(int type, byte[] payload) {
        if (closed) return;
        byte[] pkt = new byte[5 + payload.length];
        pkt[0] = (byte) type;
        pkt[1] = (byte) (payload.length >>> 24); pkt[2] = (byte) (payload.length >>> 16);
        pkt[3] = (byte) (payload.length >>> 8); pkt[4] = (byte) payload.length;
        System.arraycopy(payload, 0, pkt, 5, payload.length);
        if (type == V_CONFIG || type == V_FRAME) qv.offer(pkt); else qa.offer(pkt);
        sem.release();
    }

    private void writeLoop() {
        try {
            byte[] cur = null;
            int off = 0;
            while (!closed) {
                byte[] a = qa.poll();                      // le son passe toujours en premier
                if (a != null) {
                    if (a[0] == 0) break;
                    if (a[0] == A_DATA) audioQ.decrementAndGet();
                    out.write(a);
                    continue;
                }
                if (cur == null) {
                    cur = qv.poll(); off = 0;
                    if (cur != null && cur[0] == V_FRAME) videoQ.decrementAndGet();
                }
                if (cur != null) {                         // le son passe entre deux images
                    out.write(cur);                        // une image entière d une traite : le flux ne doit jamais être entrecoupé
                    cur = null;
                    continue;
                }
                sem.tryAcquire(20, java.util.concurrent.TimeUnit.MILLISECONDS);
            }
        } catch (Exception e) { /* fin */ }
        close();
    }

    private void putVideo(byte[] payload, boolean key) {
        if (closed) return;
        if (waitKey && !key) return;
        if (key) waitKey = false;
        if (videoQ.get() >= 8) {                       // réseau trop lent : on jette l'image en retard
            for (Iterator<byte[]> it = qv.iterator(); it.hasNext(); ) {
                byte[] p = it.next();
                if (p[0] == V_FRAME) { it.remove(); videoQ.decrementAndGet(); }
            }
            if (!key) { waitKey = true; requestKey(); return; }
        }
        videoQ.incrementAndGet();
        put(V_FRAME, payload);
    }

    private void putAudio(byte[] pcm) {
        if (audioQ.get() >= 200) {
            for (Iterator<byte[]> it = qa.iterator(); it.hasNext() && audioQ.get() > 100; ) {
                byte[] p = it.next();
                if (p[0] == A_DATA) { it.remove(); audioQ.decrementAndGet(); }
            }
        }
        audioQ.incrementAndGet();
        put(A_DATA, pcm);
    }

    // ---------------------------------------------------------------- vidéo
    void requestKey() {
        synchronized (vLock) {
            if (enc != null) {
                try {
                    Bundle bd = new Bundle();
                    bd.putInt(MediaCodec.PARAMETER_KEY_REQUEST_SYNC_FRAME, 0);
                    enc.setParameters(bd);
                } catch (Exception e) { }
            }
        }
    }

    /** (Re)démarre l'encodeur, aussi appelé quand l'écran tourne. */
    void startVideo() {
        synchronized (vLock) {
            stopVideoLocked();
            if (closed) return;
            try {
                int[] d = svc.encodedSize(maxSize);
                encW = d[0]; encH = d[1];
                MediaFormat f = MediaFormat.createVideoFormat("video/avc", encW, encH);
                f.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
                f.setInteger(MediaFormat.KEY_BIT_RATE, kbps * 1000);
                f.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
                f.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
                f.setLong(MediaFormat.KEY_REPEAT_PREVIOUS_FRAME_AFTER, 100000L);
                f.setInteger(MediaFormat.KEY_PRIORITY, 0);
                if (Build.VERSION.SDK_INT >= 30) f.setInteger(MediaFormat.KEY_LATENCY, 1);
                enc = MediaCodec.createEncoderByType("video/avc");
                enc.configure(f, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
                Surface s = enc.createInputSurface();
                enc.start();
                svc.attachSurface(s, encW, encH);
                waitKey = true;
                config = null;
                drainStop = false;
                final MediaCodec e = enc;
                drain = new Thread(new Runnable() { public void run() { drainLoop(e); } }, "SL-video");
                drain.start();
            } catch (Exception ex) {
                stopVideoLocked();
                put(INFO, ("error=" + ex.getClass().getSimpleName() + " " + ex.getMessage() + "\n").getBytes());
            }
        }
    }

    private void stopVideo() { synchronized (vLock) { stopVideoLocked(); } }

    private void stopVideoLocked() {
        drainStop = true;
        if (drain != null) { try { drain.join(2000); } catch (Exception e) { } drain = null; }
        if (enc != null) {
            try { enc.stop(); } catch (Exception e) { }
            try { enc.release(); } catch (Exception e) { }
            enc = null;
            svc.detachSurface();
        }
    }

    private void drainLoop(MediaCodec e) {
        MediaCodec.BufferInfo bi = new MediaCodec.BufferInfo();
        try {
            while (!drainStop && !closed) {
                int i = e.dequeueOutputBuffer(bi, 10000);
                if (i < 0) continue;
                ByteBuffer bb = e.getOutputBuffer(i);
                byte[] data = new byte[bi.size];
                if (bb != null) { bb.position(bi.offset); bb.limit(bi.offset + bi.size); bb.get(data); }
                boolean cfg = (bi.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0;
                boolean key = (bi.flags & MediaCodec.BUFFER_FLAG_KEY_FRAME) != 0;
                long pts = bi.presentationTimeUs;
                e.releaseOutputBuffer(i, false);
                if (cfg) {
                    config = data;
                    ByteBuffer m = ByteBuffer.allocate(4 + data.length);
                    m.putShort((short) encW).putShort((short) encH).put(data);
                    put(V_CONFIG, m.array());
                } else if (data.length > 0) {
                    byte[] c = config;
                    int cl = (key && c != null) ? c.length : 0;
                    ByteBuffer m = ByteBuffer.allocate(9 + cl + data.length);
                    m.putLong(pts).put((byte) (key ? 1 : 0));
                    if (cl > 0) m.put(c);
                    m.put(data);
                    putVideo(m.array(), key);
                }
            }
        } catch (Exception ex) { /* arrêt */ }
    }

    // ---------------------------------------------------------------- son
    private void startAudio() {
        if (Build.VERSION.SDK_INT < 29) return;
        try {
            AudioPlaybackCaptureConfiguration cfg = new AudioPlaybackCaptureConfiguration.Builder(svc.projection())
                    .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                    .addMatchingUsage(AudioAttributes.USAGE_GAME)
                    .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
                    .build();
            AudioFormat af = new AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                    .setSampleRate(RATE).setChannelMask(AudioFormat.CHANNEL_IN_STEREO).build();
            rec = new AudioRecord.Builder().setAudioFormat(af).setAudioPlaybackCaptureConfig(cfg)
                    .setBufferSizeInBytes(RATE * 4 / 5).build();
            rec.startRecording();
            ByteBuffer m = ByteBuffer.allocate(5);
            m.putInt(RATE).put((byte) 2);
            put(A_CONFIG, m.array());
            audioStop = false;
            final AudioRecord r = rec;
            audioThread = new Thread(new Runnable() { public void run() {
                android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_URGENT_AUDIO);
                byte[] buf = new byte[RATE / 50 * 4];      // 20 ms
                while (!audioStop && !closed) {
                    int n = r.read(buf, 0, buf.length);
                    if (n <= 0) { if (n < 0) break; continue; }
                    byte[] c = new byte[n];
                    System.arraycopy(buf, 0, c, 0, n);
                    putAudio(c);
                }
            } }, "SL-audio");
            audioThread.start();
        } catch (Exception ex) {
            rec = null;
            put(INFO, ("error=audio " + ex.getClass().getSimpleName() + " " + ex.getMessage() + "\n").getBytes());
        }
    }

    private void stopAudio() {
        audioStop = true;
        if (audioThread != null) { try { audioThread.join(1000); } catch (Exception e) { } audioThread = null; }
        if (rec != null) {
            try { rec.stop(); } catch (Exception e) { }
            try { rec.release(); } catch (Exception e) { }
            rec = null;
        }
    }
}
