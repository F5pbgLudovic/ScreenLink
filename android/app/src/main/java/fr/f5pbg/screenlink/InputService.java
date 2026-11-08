package fr.f5pbg.screenlink;

import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.GestureDescription;
import android.content.Context;
import android.graphics.Path;
import android.hardware.display.DisplayManager;
import android.media.AudioManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.PowerManager;
import android.util.DisplayMetrics;
import android.view.Display;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;

/** Reproduit les actions de la souris et du clavier du PC sur le téléphone (service d'accessibilité). */
public class InputService extends AccessibilityService {
    static volatile InputService inst;

    static final int KEY_BACK = 1, KEY_HOME = 2, KEY_RECENTS = 3, KEY_NOTIF = 4, KEY_VOLUP = 5, KEY_VOLDOWN = 6,
            KEY_WAKE = 7, KEY_QUICK = 8, KEY_ENTER = 9, KEY_BACKSPACE = 10, KEY_LOCK = 11, KEY_POWER = 12;

    private final Handler h = new Handler(Looper.getMainLooper());

    // état du doigt virtuel
    private boolean active;           // un doigt est posé
    private boolean busy;             // un geste est en cours de traitement
    private boolean upPending;
    private boolean movePending;
    private float curX, curY;         // dernière position envoyée (pixels)
    private float tgtX, tgtY;         // position souhaitée (pixels)
    private GestureDescription.StrokeDescription stroke;
    private long lastDispatch;
    private int token;                // invalide les rappels d'un doigt précédent

    @Override public void onServiceConnected() { inst = this; }
    @Override public void onAccessibilityEvent(AccessibilityEvent e) { }
    @Override public void onInterrupt() { }
    @Override public boolean onUnbind(android.content.Intent i) { inst = null; return super.onUnbind(i); }
    @Override public void onDestroy() { inst = null; super.onDestroy(); }

    private int[] screen() {
        DisplayMetrics m = new DisplayMetrics();
        DisplayManager dm = (DisplayManager) getSystemService(Context.DISPLAY_SERVICE);
        Display d = dm.getDisplay(Display.DEFAULT_DISPLAY);
        d.getRealMetrics(m);
        return new int[]{m.widthPixels, m.heightPixels};
    }

    private static Path pt(float x, float y) { Path p = new Path(); p.moveTo(x, y); return p; }

    // ---------- Doigt ----------
    void touch(final int action, final float nx, final float ny) {
        h.post(new Runnable() { public void run() { doTouch(action, nx, ny); } });
    }

    private void doTouch(int action, float nx, float ny) {
        int[] s = screen();
        float x = Math.max(0f, Math.min(1f, nx)) * (s[0] - 1);
        float y = Math.max(0f, Math.min(1f, ny)) * (s[1] - 1);
        if (action == 0) {                       // appui
            if (active) { release(); }
            token++;
            active = true; upPending = false; movePending = false;
            curX = x; curY = y; tgtX = x; tgtY = y;
            busy = true;
            try {
                stroke = new GestureDescription.StrokeDescription(pt(x, y), 0, 1, true);
            } catch (Exception e) { active = false; busy = false; return; }
            lastDispatch = System.currentTimeMillis();
            send(stroke);
        } else if (action == 1) {                // déplacement
            if (!active) return;
            tgtX = x; tgtY = y; movePending = true;
            flush();
        } else {                                 // relâchement
            if (!active) return;
            tgtX = x; tgtY = y; upPending = true;
            flush();
        }
    }

    private void release() { active = false; busy = false; upPending = false; movePending = false; stroke = null; token++; }

    private void send(GestureDescription.StrokeDescription sd) {
        final int my = token;
        boolean ok;
        try {
            GestureDescription g = new GestureDescription.Builder().addStroke(sd).build();
            ok = dispatchGesture(g, new GestureResultCallback() {
                @Override public void onCompleted(GestureDescription gd) { if (my == token) { busy = false; flush(); } }
                @Override public void onCancelled(GestureDescription gd) { if (my == token) release(); }
            }, h);
        } catch (Exception e) { ok = false; }
        if (!ok) release();
    }

    private void flush() {
        if (!active || busy) return;
        long now = System.currentTimeMillis();
        if (upPending || movePending) {
            Path p = new Path();
            p.moveTo(curX, curY);
            p.lineTo(tgtX, tgtY);
            long dur = Math.max(1, Math.min(60, now - lastDispatch));
            boolean last = upPending;
            try {
                stroke = stroke.continueStroke(p, 0, dur, !last);
            } catch (Exception e) { release(); return; }
            curX = tgtX; curY = tgtY; lastDispatch = now;
            movePending = false;
            busy = true;
            if (last) { upPending = false; final GestureDescription.StrokeDescription sd = stroke; send(sd); active = false; return; }
            send(stroke);
        } else {
            // doigt immobile : on prolonge le contact (appui long) par petits morceaux
            final int my = token;
            h.postDelayed(new Runnable() { public void run() {
                if (my != token || !active || busy || upPending || movePending) return;
                long n = System.currentTimeMillis();
                Path p = new Path(); p.moveTo(curX, curY); p.lineTo(curX, curY);
                try { stroke = stroke.continueStroke(p, 0, 100, true); } catch (Exception e) { release(); return; }
                lastDispatch = n; busy = true; send(stroke);
            } }, 150);
        }
    }

    // ---------- Molette : défilement ou pincement ----------
    void scroll(final float nx, final float ny, final int dx, final int dy, final boolean ctrl) {
        h.post(new Runnable() { public void run() { doScroll(nx, ny, dx, dy, ctrl); } });
    }

    private void doScroll(float nx, float ny, int dx, int dy, boolean ctrl) {
        if (active) return;
        int[] s = screen();
        float x = nx * (s[0] - 1), y = ny * (s[1] - 1);
        try {
            GestureDescription.Builder b = new GestureDescription.Builder();
            if (ctrl) {                                   // pincement : dy>0 = agrandir
                float d0 = Math.min(s[0], s[1]) * 0.08f, d1 = Math.min(s[0], s[1]) * (dy > 0 ? 0.28f : 0.02f);
                if (dy < 0) { float t = d0; d0 = Math.min(s[0], s[1]) * 0.28f; d1 = Math.min(s[0], s[1]) * 0.08f; }
                Path a = new Path(); a.moveTo(x - d0, y); a.lineTo(x - d1, y);
                Path c = new Path(); c.moveTo(x + d0, y); c.lineTo(x + d1, y);
                b.addStroke(new GestureDescription.StrokeDescription(a, 0, 250));
                b.addStroke(new GestureDescription.StrokeDescription(c, 0, 250));
            } else {
                float stepY = s[1] * 0.14f, stepX = s[0] * 0.14f;
                float ex = x + dx * stepX, ey = y + dy * stepY;
                ex = Math.max(1, Math.min(s[0] - 2, ex)); ey = Math.max(1, Math.min(s[1] - 2, ey));
                Path p = new Path(); p.moveTo(x, y); p.lineTo(ex, ey);
                b.addStroke(new GestureDescription.StrokeDescription(p, 0, 140));
            }
            dispatchGesture(b.build(), null, null);
        } catch (Exception e) { /* ignoré */ }
    }

    // ---------- Touches ----------
    void key(final int code) {
        h.post(new Runnable() { public void run() { doKey(code); } });
    }

    private void doKey(int code) {
        switch (code) {
            case KEY_BACK: performGlobalAction(GLOBAL_ACTION_BACK); break;
            case KEY_HOME: performGlobalAction(GLOBAL_ACTION_HOME); break;
            case KEY_RECENTS: performGlobalAction(GLOBAL_ACTION_RECENTS); break;
            case KEY_NOTIF: performGlobalAction(GLOBAL_ACTION_NOTIFICATIONS); break;
            case KEY_QUICK: performGlobalAction(GLOBAL_ACTION_QUICK_SETTINGS); break;
            case KEY_POWER: performGlobalAction(GLOBAL_ACTION_POWER_DIALOG); break;
            case KEY_LOCK: if (Build.VERSION.SDK_INT >= 28) performGlobalAction(GLOBAL_ACTION_LOCK_SCREEN); break;
            case KEY_VOLUP:
            case KEY_VOLDOWN: {
                AudioManager am = (AudioManager) getSystemService(Context.AUDIO_SERVICE);
                am.adjustStreamVolume(AudioManager.STREAM_MUSIC,
                        code == KEY_VOLUP ? AudioManager.ADJUST_RAISE : AudioManager.ADJUST_LOWER, AudioManager.FLAG_SHOW_UI);
                break;
            }
            case KEY_WAKE: wake(); break;
            case KEY_ENTER: enter(); break;
            case KEY_BACKSPACE: backspace(); break;
        }
    }

    @SuppressWarnings("deprecation")
    private void wake() {
        try {
            PowerManager pm = (PowerManager) getSystemService(Context.POWER_SERVICE);
            PowerManager.WakeLock w = pm.newWakeLock(PowerManager.SCREEN_BRIGHT_WAKE_LOCK | PowerManager.ACQUIRE_CAUSES_WAKEUP, "ScreenLink:wake");
            w.acquire(3000);
        } catch (Exception e) { /* ignoré */ }
    }

    private AccessibilityNodeInfo focused() {
        AccessibilityNodeInfo root = getRootInActiveWindow();
        if (root == null) return null;
        AccessibilityNodeInfo f = root.findFocus(AccessibilityNodeInfo.FOCUS_INPUT);
        return f;
    }

    private static String textOf(AccessibilityNodeInfo n) {
        CharSequence t = n.getText();
        if (t == null) return "";
        if (Build.VERSION.SDK_INT >= 26 && n.isShowingHintText()) return "";
        return t.toString();
    }

    void text(final String s) {
        h.post(new Runnable() { public void run() { edit(s, false); } });
    }

    private void backspace() { edit("", true); }

    private void enter() {
        AccessibilityNodeInfo n = focused();
        if (n == null) return;
        if (Build.VERSION.SDK_INT >= 30) {
            if (n.performAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_IME_ENTER.getId())) return;
        }
        if (n.isMultiLine()) edit("\n", false);
    }

    private void edit(String ins, boolean back) {
        AccessibilityNodeInfo n = focused();
        if (n == null || !n.isEditable()) return;
        String cur = textOf(n);
        int a = n.getTextSelectionStart(), b = n.getTextSelectionEnd();
        if (a < 0 || b < 0 || a > cur.length() || b > cur.length()) { a = cur.length(); b = a; }
        if (a > b) { int t = a; a = b; b = t; }
        String res; int caret;
        if (back) {
            if (a != b) { res = cur.substring(0, a) + cur.substring(b); caret = a; }
            else if (a > 0) { res = cur.substring(0, a - 1) + cur.substring(a); caret = a - 1; }
            else return;
        } else {
            res = cur.substring(0, a) + ins + cur.substring(b); caret = a + ins.length();
        }
        Bundle args = new Bundle();
        args.putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, res);
        n.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args);
        Bundle sel = new Bundle();
        sel.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, caret);
        sel.putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, caret);
        n.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, sel);
    }
}
