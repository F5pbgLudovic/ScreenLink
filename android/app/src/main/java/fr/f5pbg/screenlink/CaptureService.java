package fr.f5pbg.screenlink;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;
import android.util.DisplayMetrics;
import android.view.Display;
import android.view.Surface;

import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;

/** Service au premier plan : détient la capture d'écran et le serveur TCP auquel le PC se connecte. */
public class CaptureService extends Service {
    static final int PORT = 27183;
    static final String ACT_STOP = "fr.f5pbg.screenlink.STOP";

    static volatile CaptureService inst;
    static volatile boolean running;

    private MediaProjection projection;
    private VirtualDisplay vd;
    private ServerSocket server;
    private Thread acceptThread;
    private Session current;
    private final Object lock = new Object();
    private DisplayManager dm;
    private DisplayManager.DisplayListener dl;
    private int lastW, lastH;
    private PowerManager.WakeLock wl;
    private final Handler h = new Handler(Looper.getMainLooper());

    private int fails;
    private long lockedUntil;

    @Override public IBinder onBind(Intent i) { return null; }

    static String pin(Context c) {
        SharedPreferences sp = c.getSharedPreferences("sl", MODE_PRIVATE);
        String p = sp.getString("pin", null);
        if (p == null) { p = newPin(c); }
        return p;
    }

    static String newPin(Context c) {
        String p = String.format("%06d", new java.security.SecureRandom().nextInt(1000000));
        c.getSharedPreferences("sl", MODE_PRIVATE).edit().putString("pin", p).apply();
        return p;
    }

    String pin() { return pin(this); }
    boolean locked() { return System.currentTimeMillis() < lockedUntil; }
    synchronized void failed() { if (++fails >= 5) { fails = 0; lockedUntil = System.currentTimeMillis() + 60000; } }
    synchronized void ok() { fails = 0; }
    MediaProjection projection() { return projection; }

    static int clients() { CaptureService s = inst; return (s != null && s.current != null && s.current.authenticated) ? 1 : 0; }

    @Override public int onStartCommand(Intent it, int flags, int id) {
        if (it != null && ACT_STOP.equals(it.getAction())) { stopSelf(); return START_NOT_STICKY; }
        if (it == null || !it.hasExtra("code")) { stopSelf(); return START_NOT_STICKY; }
        if (inst != null) return START_NOT_STICKY;

        String ch = "sl";
        NotificationManager nm = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        nm.createNotificationChannel(new NotificationChannel(ch, "ScreenLink", NotificationManager.IMPORTANCE_LOW));
        PendingIntent open = PendingIntent.getActivity(this, 0, new Intent(this, MainActivity.class), PendingIntent.FLAG_IMMUTABLE);
        PendingIntent stop = PendingIntent.getService(this, 1, new Intent(this, CaptureService.class).setAction(ACT_STOP), PendingIntent.FLAG_IMMUTABLE);
        Notification n = new Notification.Builder(this, ch)
                .setSmallIcon(R.drawable.ic_stat)
                .setContentTitle("ScreenLink")
                .setContentText(MainActivity.fr() ? "Partage de l'écran actif — port " + PORT : "Screen sharing active — port " + PORT)
                .setContentIntent(open)
                .addAction(new Notification.Action.Builder(null, MainActivity.fr() ? "Arrêter" : "Stop", stop).build())
                .setOngoing(true).build();
        if (android.os.Build.VERSION.SDK_INT >= 29)
            startForeground(1, n, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
        else startForeground(1, n);

        MediaProjectionManager mpm = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        projection = mpm.getMediaProjection(it.getIntExtra("code", 0), (Intent) it.getParcelableExtra("data"));
        if (projection == null) { stopSelf(); return START_NOT_STICKY; }
        projection.registerCallback(new MediaProjection.Callback() {
            @Override public void onStop() { h.post(new Runnable() { public void run() { stopSelf(); } }); }
        }, h);

        try {
            PowerManager pm = (PowerManager) getSystemService(POWER_SERVICE);
            wl = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "ScreenLink:cpu");
            wl.acquire();
        } catch (Exception e) { }

        dm = (DisplayManager) getSystemService(DISPLAY_SERVICE);
        int[] s = realSize();
        lastW = s[0]; lastH = s[1];
        dl = new DisplayManager.DisplayListener() {
            @Override public void onDisplayAdded(int d) { }
            @Override public void onDisplayRemoved(int d) { }
            @Override public void onDisplayChanged(int d) {
                if (d != Display.DEFAULT_DISPLAY) return;
                int[] n = realSize();
                if (n[0] != lastW || n[1] != lastH) {
                    lastW = n[0]; lastH = n[1];
                    final Session cs = current;
                    if (cs != null) new Thread(new Runnable() { public void run() { cs.startVideo(); } }).start();
                }
            }
        };
        dm.registerDisplayListener(dl, h);

        inst = this;
        running = true;
        acceptThread = new Thread(new Runnable() { public void run() { acceptLoop(); } }, "SL-accept");
        acceptThread.start();
        return START_NOT_STICKY;
    }

    private void acceptLoop() {
        try {
            server = new ServerSocket();
            server.setReuseAddress(true);
            server.bind(new InetSocketAddress(PORT));
            while (!server.isClosed()) {
                Socket s = server.accept();
                new Thread(new Session(this, s), "SL-session").start();
            }
        } catch (Exception e) { /* fermé */ }
    }

    boolean attach(Session s) {
        synchronized (lock) {
            if (current != null && !current.isClosed()) return false;
            current = s;
            return true;
        }
    }

    void detach(Session s) {
        synchronized (lock) { if (current == s) current = null; }
    }

    int[] realSize() {
        DisplayMetrics m = new DisplayMetrics();
        dm.getDisplay(Display.DEFAULT_DISPLAY).getRealMetrics(m);
        return new int[]{m.widthPixels, m.heightPixels, m.densityDpi};
    }

    /** Taille de l'image envoyée : écran réduit à maxSize, multiple de 16. */
    int[] encodedSize(int maxSize) {
        int[] r = realSize();
        float sc = Math.min(1f, maxSize / (float) Math.max(r[0], r[1]));
        int w = Math.max(16, Math.round(r[0] * sc / 16f) * 16);
        int hh = Math.max(16, Math.round(r[1] * sc / 16f) * 16);
        return new int[]{w, hh};
    }

    void attachSurface(Surface s, int w, int hh) {
        int dpi = realSize()[2];
        if (vd == null) {
            vd = projection.createVirtualDisplay("ScreenLink", w, hh, dpi,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR, s, null, null);
        } else {
            vd.resize(w, hh, dpi);
            vd.setSurface(s);
        }
    }

    void detachSurface() {
        if (vd != null) { try { vd.setSurface(null); } catch (Exception e) { } }
    }

    @Override public void onDestroy() {
        running = false;
        inst = null;
        try { if (server != null) server.close(); } catch (Exception e) { }
        Session c = current;
        if (c != null) c.close();
        try { if (dm != null && dl != null) dm.unregisterDisplayListener(dl); } catch (Exception e) { }
        try { if (vd != null) vd.release(); } catch (Exception e) { }
        try { if (projection != null) projection.stop(); } catch (Exception e) { }
        try { if (wl != null && wl.isHeld()) wl.release(); } catch (Exception e) { }
        super.onDestroy();
    }
}
