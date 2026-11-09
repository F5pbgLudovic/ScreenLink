package fr.f5pbg.screenlink;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Typeface;
import android.media.projection.MediaProjectionConfig;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.Locale;

public class MainActivity extends Activity {
    static boolean fr() { return Locale.getDefault().getLanguage().equals("fr"); }
    static String T(String f, String e) { return fr() ? f : e; }

    private String ver() { try { return getPackageManager().getPackageInfo(getPackageName(), 0).versionName; } catch (Exception e) { return "1.0"; } }
    private static final int REQ_PROJ = 1, REQ_PERM = 2;
    private TextView tvStatus, tvIp, tvPin, tvAccess, tvHelp;
    private Button btnStart, btnAccess, btnPin, btnDnd;
    private final Handler h = new Handler(Looper.getMainLooper());
    private boolean wantStart;

    private final Runnable tick = new Runnable() { public void run() { refresh(); h.postDelayed(this, 1000); } };

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        int pad = (int) (16 * getResources().getDisplayMetrics().density);
        ScrollView sv = new ScrollView(this);
        sv.setBackgroundColor(Color.rgb(18, 22, 30));
        LinearLayout ll = new LinearLayout(this);
        ll.setOrientation(LinearLayout.VERTICAL);
        ll.setPadding(pad, pad * 2, pad, pad);
        sv.addView(ll);

        TextView title = new TextView(this);
        title.setText("ScreenLink V" + ver() + " @F5PBG 2026");
        title.setTextSize(22); title.setTypeface(Typeface.DEFAULT_BOLD); title.setTextColor(Color.WHITE);
        ll.addView(title);
        TextView sub = new TextView(this);
        sub.setText(T("Voir et commander ce téléphone depuis le PC (Wi-Fi ou USB)", "View and control this phone from the PC (Wi-Fi or USB)"));
        sub.setTextColor(Color.rgb(160, 175, 200)); sub.setPadding(0, 0, 0, pad);
        ll.addView(sub);

        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        ll.addView(row, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        tvStatus = new TextView(this);
        tvStatus.setTextSize(20); tvStatus.setTextColor(Color.WHITE); tvStatus.setPadding(0, 12, 0, 12);
        row.addView(tvStatus, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        float dn = getResources().getDisplayMetrics().density;
        final float dd = dn;
        View quit = new View(this) {
            @Override protected void onDraw(android.graphics.Canvas c) {
                android.graphics.Paint p = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
                float w = getWidth(), hh = getHeight(), r = Math.min(w, hh) / 2f;
                p.setColor(Color.rgb(220, 38, 38));
                c.drawCircle(w / 2f, hh / 2f, r, p);
                p.setColor(Color.WHITE);
                float q = r * 0.42f;
                c.drawRoundRect(new android.graphics.RectF(w / 2f - q, hh / 2f - q, w / 2f + q, hh / 2f + q), dd * 3, dd * 3, p);
            }
        };
        quit.setContentDescription(T("Arrêter et quitter ScreenLink", "Stop and quit ScreenLink"));
        quit.setOnClickListener(new View.OnClickListener() { public void onClick(View v) { quitAll(); } });
        row.addView(quit, new LinearLayout.LayoutParams((int) (48 * dn), (int) (48 * dn)));
        row.setClipChildren(false); ll.setClipChildren(false); sv.setClipChildren(false); sv.setClipToPadding(false);
        quit.setTranslationY(-6 * dn);   // centré entre le sous-titre et le bouton « Démarrer »
        btnStart = button(ll, "");
        btnStart.setOnClickListener(new View.OnClickListener() { public void onClick(View v) { toggle(); } });

        tvIp = text(ll, 16, Color.rgb(190, 205, 230));
        tvPin = text(ll, 30, Color.rgb(255, 214, 79));
        tvPin.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        btnPin = button(ll, T("Nouveau code", "New code"));
        btnPin.setOnClickListener(new View.OnClickListener() { public void onClick(View v) { CaptureService.newPin(MainActivity.this); refresh(); } });

        tvAccess = text(ll, 16, Color.WHITE);
        btnAccess = button(ll, T("Activer la commande à la souris", "Enable mouse control"));
        btnAccess.setOnClickListener(new View.OnClickListener() { public void onClick(View v) {
            startActivity(new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS));
        } });

        btnDnd = button(ll, T("Autoriser « Ne pas déranger » (case Pas de notif)", "Allow “Do not disturb” (No notif checkbox)"));
        btnDnd.setOnClickListener(new View.OnClickListener() { public void onClick(View v) {
            startActivity(new Intent(Settings.ACTION_NOTIFICATION_POLICY_ACCESS_SETTINGS));
        } });

        tvHelp = text(ll, 14, Color.rgb(160, 175, 200));
        tvHelp.setPadding(0, pad, 0, 0);
        tvHelp.setText(T(
                "1. Touchez « Démarrer le partage » puis acceptez la capture de l'écran entier.\n"
              + "2. Touchez « Activer la commande », choisissez ScreenLink dans la liste des services (Applications installées) et activez-le : c'est ce qui permet à la souris du PC de toucher l'écran.\n"
              + "3. Sur le PC, lancez ScreenLinkPC, saisissez l'adresse IP ci-dessus et le code (Wi-Fi), ou choisissez USB.\n\n"
              + "Le contenu protégé (applications bancaires, certaines vidéos) apparaît noir et certaines applications interdisent la capture du son.",
                "1. Tap “Start sharing” and accept capturing the entire screen.\n"
              + "2. Tap “Enable control”, pick ScreenLink in the services list (Installed apps) and turn it on: this lets the PC mouse touch the screen.\n"
              + "3. On the PC, run ScreenLinkPC, enter the IP address above and the code (Wi-Fi), or choose USB.\n\n"
              + "Protected content (banking apps, some videos) shows black and some apps forbid audio capture."));
        setContentView(sv);

        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS}, 99);
        handleIntent(getIntent());
    }

    @Override protected void onNewIntent(Intent i) { super.onNewIntent(i); handleIntent(i); }

    /** adb shell am start ... --ez start true  : démarre directement (utilisé par le mode USB du PC). */
    private void handleIntent(Intent i) {
        if (i != null && i.getBooleanExtra("start", false) && !CaptureService.running) { i.removeExtra("start"); toggle(); }
    }

    private TextView text(LinearLayout ll, int sp, int color) {
        TextView t = new TextView(this);
        t.setTextSize(sp); t.setTextColor(color); t.setPadding(0, 12, 0, 12);
        t.setTextIsSelectable(true);
        ll.addView(t);
        return t;
    }

    private Button button(LinearLayout ll, String s) {
        Button bt = new Button(this);
        bt.setText(s);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.gravity = Gravity.CENTER;
        ll.addView(bt, lp);
        return bt;
    }

    @Override protected void onResume() { super.onResume(); h.post(tick); }
    @Override protected void onPause() { super.onPause(); h.removeCallbacks(tick); }

    /** Arrête le partage (le service rétablit volume et « Ne pas déranger »), ferme l'écran et termine le processus. */
    private void quitAll() {
        try { stopService(new Intent(this, CaptureService.class)); } catch (Exception e) { }
        h.removeCallbacks(tick);
        finishAndRemoveTask();
        h.postDelayed(new Runnable() { public void run() { android.os.Process.killProcess(android.os.Process.myPid()); } }, 700);
    }

    private void toggle() {
        if (CaptureService.running) {
            startService(new Intent(this, CaptureService.class).setAction(CaptureService.ACT_STOP));
            return;
        }
        if (Build.VERSION.SDK_INT >= 23 && checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            wantStart = true;
            requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO}, REQ_PERM);
            return;
        }
        askProjection();
    }

    private void askProjection() {
        MediaProjectionManager m = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        Intent it = Build.VERSION.SDK_INT >= 34
                ? m.createScreenCaptureIntent(MediaProjectionConfig.createConfigForDefaultDisplay())
                : m.createScreenCaptureIntent();
        startActivityForResult(it, REQ_PROJ);
    }

    @Override public void onRequestPermissionsResult(int rc, String[] p, int[] r) {
        if (rc == REQ_PERM && wantStart) { wantStart = false; askProjection(); }   // le son reste optionnel
    }

    @Override protected void onActivityResult(int rc, int res, Intent data) {
        if (rc == REQ_PROJ && res == RESULT_OK && data != null) {
            Intent s = new Intent(this, CaptureService.class).putExtra("code", res).putExtra("data", data);
            startForegroundService(s);
            h.postDelayed(new Runnable() { public void run() { refresh(); } }, 500);
        }
    }

    private String ips() {
        StringBuilder sb = new StringBuilder();
        try {
            for (NetworkInterface ni : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                if (!ni.isUp() || ni.isLoopback()) continue;
                for (InetAddress a : Collections.list(ni.getInetAddresses()))
                    if (a instanceof Inet4Address) sb.append(a.getHostAddress()).append("   (").append(ni.getName()).append(")\n");
            }
        } catch (Exception e) { }
        return sb.length() == 0 ? T("(aucun réseau)", "(no network)") : sb.toString().trim();
    }

    private void refresh() {
        boolean run = CaptureService.running;
        boolean acc = InputService.inst != null;
        int cl = CaptureService.clients();
        tvStatus.setText(run ? (cl > 0 ? T("● PC connecté", "● PC connected") : T("● En attente du PC", "● Waiting for the PC"))
                             : T("○ Partage arrêté", "○ Sharing stopped"));
        tvStatus.setTextColor(run ? (cl > 0 ? Color.rgb(90, 220, 120) : Color.rgb(255, 200, 80)) : Color.rgb(200, 200, 200));
        btnStart.setText(run ? T("Arrêter le partage", "Stop sharing") : T("Démarrer le partage", "Start sharing"));
        tvIp.setText(T("Adresse IP (Wi-Fi) :\n", "IP address (Wi-Fi):\n") + ips() + "\nPort " + CaptureService.PORT);
        tvPin.setText(T("Code : ", "Code: ") + CaptureService.pin(this));
        tvAccess.setText(acc ? T("✔ Commande à la souris activée", "✔ Mouse control enabled")
                             : T("✘ Commande à la souris non activée (vous ne verrez l'écran que sans pouvoir le toucher)",
                                 "✘ Mouse control not enabled (view only)"));
        tvAccess.setTextColor(acc ? Color.rgb(90, 220, 120) : Color.rgb(255, 150, 120));
        btnAccess.setVisibility(acc ? View.GONE : View.VISIBLE);
        btnDnd.setVisibility(CaptureService.dndAllowed(this) ? View.GONE : View.VISIBLE);
    }
}
