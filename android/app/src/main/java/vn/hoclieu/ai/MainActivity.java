package vn.hoclieu.ai;

import android.app.Activity;
import android.app.ActivityManager;
import android.content.ClipData;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.util.Base64;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends Activity {
    static final String HOST = "appassets.androidplatform.net";
    static final String START = "https://" + HOST + "/assets/www/index.html";
    static final int REQ_FILE = 11, REQ_MODEL = 12, REQ_SAVE = 13;

    WebView wv;
    ValueCallback<Uri[]> fileCb;
    byte[] pendingSave;
    final ExecutorService pool = Executors.newCachedThreadPool();
    final Map<String, HttpURLConnection> lanConns = new ConcurrentHashMap<>();

    File modelDir() {
        File d = new File(getFilesDir(), "models");
        if (!d.exists()) d.mkdirs();
        return d;
    }

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        wv = new WebView(this);
        wv.setBackgroundColor(Color.WHITE);
        setContentView(wv);

        WebSettings s = wv.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setDatabaseEnabled(true);
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(true);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        s.setTextZoom(100);
        s.setUserAgentString(s.getUserAgentString() + " HocLieuAI/1.0");

        wv.addJavascriptInterface(new Bridge(), "AndroidBridge");
        wv.addJavascriptInterface(new Saver(), "AndroidSave");
        wv.addJavascriptInterface(new Lan(), "AndroidLan");

        wv.setWebViewClient(new WebViewClient() {
            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest req) {
                Uri u = req.getUrl();
                if (!HOST.equals(u.getHost())) return null;
                return serveLocal(u.getPath());
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest req) {
                return !HOST.equals(req.getUrl().getHost());
            }
        });

        wv.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onShowFileChooser(WebView w, ValueCallback<Uri[]> cb, FileChooserParams p) {
                if (fileCb != null) fileCb.onReceiveValue(null);
                fileCb = cb;
                Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                i.addCategory(Intent.CATEGORY_OPENABLE);
                i.setType("*/*");
                i.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, p.getMode() == FileChooserParams.MODE_OPEN_MULTIPLE);
                try {
                    startActivityForResult(i, REQ_FILE);
                } catch (Exception e) {
                    fileCb = null;
                    toast("Không mở được trình chọn file");
                    return false;
                }
                return true;
            }
        });

        if (b != null) wv.restoreState(b);
        else wv.loadUrl(START);
    }

    @Override
    protected void onSaveInstanceState(Bundle out) {
        super.onSaveInstanceState(out);
        wv.saveState(out);
    }

    /* ---------- phục vụ file trong APK và file mô hình ---------- */
    static String mime(String path) {
        String p = path.toLowerCase(Locale.ROOT);
        if (p.endsWith(".html")) return "text/html";
        if (p.endsWith(".js") || p.endsWith(".mjs")) return "text/javascript";
        if (p.endsWith(".css")) return "text/css";
        if (p.endsWith(".wasm")) return "application/wasm";
        if (p.endsWith(".json")) return "application/json";
        if (p.endsWith(".svg")) return "image/svg+xml";
        if (p.endsWith(".png")) return "image/png";
        if (p.endsWith(".gz")) return "application/gzip";
        return "application/octet-stream";
    }

    WebResourceResponse serveLocal(String path) {
        Map<String, String> h = new HashMap<>();
        h.put("Cross-Origin-Opener-Policy", "same-origin");
        h.put("Cross-Origin-Embedder-Policy", "require-corp");
        h.put("Cross-Origin-Resource-Policy", "same-origin");
        h.put("Access-Control-Allow-Origin", "*");
        try {
            InputStream in;
            String m;
            if (path.startsWith("/assets/")) {
                String a = path.substring("/assets/".length());
                in = getAssets().open(a);
                m = mime(a);
                if (m.equals("text/html")) h.put("Cache-Control", "no-cache");
            } else if (path.startsWith("/model/")) {
                String name = new File(Uri.decode(path.substring("/model/".length()))).getName();
                File f = new File(modelDir(), name);
                if (!f.exists()) return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", h, null);
                in = new FileInputStream(f);
                m = "application/octet-stream";
                h.put("Content-Length", String.valueOf(f.length()));
            } else {
                return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", h, null);
            }
            String enc = m.startsWith("text/") || m.equals("application/json") ? "utf-8" : null;
            return new WebResourceResponse(m, enc, 200, "OK", h, in);
        } catch (Exception e) {
            return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", h, null);
        }
    }

    /* ---------- gọi JS ---------- */
    static String q(String s) {
        if (s == null) return "null";
        return JSONObject.quote(s).replace("\u2028", "\\u2028").replace("\u2029", "\\u2029");
    }

    void js(final String code) {
        runOnUiThread(() -> { if (wv != null) wv.evaluateJavascript(code, null); });
    }

    void toast(final String m) {
        runOnUiThread(() -> Toast.makeText(this, m, Toast.LENGTH_LONG).show());
    }

    String displayName(Uri u) {
        String n = null;
        try (Cursor c = getContentResolver().query(u, null, null, null, null)) {
            if (c != null && c.moveToFirst()) {
                int i = c.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (i >= 0) n = c.getString(i);
            }
        } catch (Exception ignored) { }
        if (n == null) n = u.getLastPathSegment();
        if (n == null) n = "model.gguf";
        return n.replaceAll("[\\\\/:*?\"<>|]", "_");
    }

    long uriSize(Uri u) {
        try (Cursor c = getContentResolver().query(u, null, null, null, null)) {
            if (c != null && c.moveToFirst()) {
                int i = c.getColumnIndex(OpenableColumns.SIZE);
                if (i >= 0 && !c.isNull(i)) return c.getLong(i);
            }
        } catch (Exception ignored) { }
        return -1;
    }

    /* ---------- kết quả chọn file ---------- */
    @Override
    protected void onActivityResult(int req, int res, Intent data) {
        super.onActivityResult(req, res, data);
        if (req == REQ_FILE) {
            if (fileCb == null) return;
            Uri[] out = null;
            if (res == RESULT_OK && data != null) {
                ClipData cd = data.getClipData();
                if (cd != null && cd.getItemCount() > 0) {
                    out = new Uri[cd.getItemCount()];
                    for (int i = 0; i < cd.getItemCount(); i++) out[i] = cd.getItemAt(i).getUri();
                } else if (data.getData() != null) {
                    out = new Uri[]{data.getData()};
                }
            }
            fileCb.onReceiveValue(out);
            fileCb = null;
        } else if (req == REQ_MODEL) {
            if (res != RESULT_OK || data == null || data.getData() == null) return;
            final Uri u = data.getData();
            final String name = displayName(u);
            if (!name.toLowerCase(Locale.ROOT).endsWith(".gguf")) {
                js("window.onModelAdded&&onModelAdded(" + q(name) + "," + q("File không phải .gguf") + ")");
                return;
            }
            final long total = uriSize(u);
            toast("Đang chép mô hình vào app, vui lòng chờ…");
            pool.execute(() -> {
                File tmp = new File(modelDir(), name + ".part");
                try (InputStream in = getContentResolver().openInputStream(u);
                     OutputStream os = new FileOutputStream(tmp)) {
                    byte[] buf = new byte[1 << 20];
                    long done = 0, lastMb = -1;
                    int n;
                    while ((n = in.read(buf)) > 0) {
                        os.write(buf, 0, n);
                        done += n;
                        long mb = done >> 20;
                        if (mb / 25 != lastMb / 25) {
                            lastMb = mb;
                            js("window.onModelProgress&&onModelProgress(" + q(name) + "," + mb + "," + (total > 0 ? (total >> 20) : 0) + ")");
                        }
                    }
                    os.flush();
                    os.close();
                    File dst = new File(modelDir(), name);
                    if (dst.exists()) dst.delete();
                    if (!tmp.renameTo(dst)) throw new Exception("Không đổi tên được file");
                    js("window.onModelAdded&&onModelAdded(" + q(name) + ",null)");
                } catch (Exception e) {
                    tmp.delete();
                    js("window.onModelAdded&&onModelAdded(" + q(name) + "," + q(String.valueOf(e.getMessage())) + ")");
                }
            });
        } else if (req == REQ_SAVE) {
            final byte[] bytes = pendingSave;
            pendingSave = null;
            if (res != RESULT_OK || data == null || data.getData() == null || bytes == null) return;
            final Uri u = data.getData();
            pool.execute(() -> {
                try (OutputStream os = getContentResolver().openOutputStream(u, "wt")) {
                    os.write(bytes);
                    toast("Đã lưu file");
                } catch (Exception e) {
                    toast("Lỗi lưu file: " + e.getMessage());
                }
            });
        }
    }

    @Override
    public void onBackPressed() {
        wv.evaluateJavascript("(window.onAndroidBack&&window.onAndroidBack())?'1':'0'", v -> {
            if (v == null || !v.contains("1")) MainActivity.super.onBackPressed();
        });
    }

    @Override
    protected void onDestroy() {
        for (HttpURLConnection c : lanConns.values()) try { c.disconnect(); } catch (Exception ignored) { }
        pool.shutdownNow();
        if (wv != null) { wv.destroy(); wv = null; }
        super.onDestroy();
    }

    /* ================= Cầu nối cho JS ================= */
    class Bridge {
        @JavascriptInterface
        public long totalRam() {
            ActivityManager am = (ActivityManager) getSystemService(Context.ACTIVITY_SERVICE);
            ActivityManager.MemoryInfo mi = new ActivityManager.MemoryInfo();
            am.getMemoryInfo(mi);
            return mi.totalMem;
        }

        @JavascriptInterface
        public String listModels() {
            JSONArray a = new JSONArray();
            File[] fs = modelDir().listFiles();
            if (fs != null) for (File f : fs) {
                if (!f.getName().toLowerCase(Locale.ROOT).endsWith(".gguf")) continue;
                try {
                    JSONObject o = new JSONObject();
                    o.put("name", f.getName());
                    o.put("size", f.length());
                    a.put(o);
                } catch (Exception ignored) { }
            }
            return a.toString();
        }

        @JavascriptInterface
        public void pickModel() {
            runOnUiThread(() -> {
                Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                i.addCategory(Intent.CATEGORY_OPENABLE);
                i.setType("*/*");
                try { startActivityForResult(i, REQ_MODEL); } catch (Exception e) { toast("Không mở được trình chọn file"); }
            });
        }

        @JavascriptInterface
        public boolean deleteModel(String name) {
            File f = new File(modelDir(), new File(name).getName());
            return f.delete();
        }

        @JavascriptInterface
        public long freeSpace() {
            return getFilesDir().getUsableSpace();
        }
    }

    class Saver {
        @JavascriptInterface
        public void saveFile(final String name, final String mime, final String b64) {
            try {
                pendingSave = Base64.decode(b64, Base64.DEFAULT);
            } catch (Exception e) {
                toast("Lỗi dữ liệu file");
                return;
            }
            runOnUiThread(() -> {
                Intent i = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                i.addCategory(Intent.CATEGORY_OPENABLE);
                i.setType(mime == null || mime.isEmpty() ? "application/octet-stream" : mime);
                i.putExtra(Intent.EXTRA_TITLE, name);
                try { startActivityForResult(i, REQ_SAVE); } catch (Exception e) { toast("Không mở được hộp thoại lưu"); }
            });
        }
    }

    /* Gọi máy chủ AI trên PC qua mạng LAN (http), trả dữ liệu về JS từng phần */
    class Lan {
        void run(final String id, final String url, final String body) {
            pool.execute(() -> {
                HttpURLConnection c = null;
                try {
                    c = (HttpURLConnection) new URL(url).openConnection();
                    lanConns.put(id, c);
                    c.setConnectTimeout(4000);
                    c.setReadTimeout(body == null ? 4000 : 600000);
                    c.setRequestProperty("Accept", body == null ? "application/json" : "text/event-stream");
                    if (body != null) {
                        c.setRequestMethod("POST");
                        c.setDoOutput(true);
                        c.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                        byte[] bb = body.getBytes(StandardCharsets.UTF_8);
                        c.setFixedLengthStreamingMode(bb.length);
                        try (OutputStream os = c.getOutputStream()) { os.write(bb); }
                    }
                    int code = c.getResponseCode();
                    if (code >= 400) throw new Exception("Máy chủ báo lỗi " + code);
                    try (InputStreamReader r = new InputStreamReader(c.getInputStream(), StandardCharsets.UTF_8)) {
                        char[] buf = new char[4096];
                        int n;
                        while ((n = r.read(buf)) > 0) {
                            if (!lanConns.containsKey(id)) break;
                            js("window.__lan&&__lan(" + q(id) + ",'data'," + q(new String(buf, 0, n)) + ")");
                        }
                    }
                    js("window.__lan&&__lan(" + q(id) + ",'done',null)");
                } catch (Exception e) {
                    if (lanConns.containsKey(id))
                        js("window.__lan&&__lan(" + q(id) + ",'error'," + q(String.valueOf(e.getMessage())) + ")");
                } finally {
                    lanConns.remove(id);
                    if (c != null) try { c.disconnect(); } catch (Exception ignored) { }
                }
            });
        }

        @JavascriptInterface
        public void get(String id, String url) { run(id, url, null); }

        @JavascriptInterface
        public void post(String id, String url, String body) { run(id, url, body); }

        @JavascriptInterface
        public void cancel(String id) {
            HttpURLConnection c = lanConns.remove(id);
            if (c != null) pool.execute(() -> { try { c.disconnect(); } catch (Exception ignored) { } });
        }
    }
}
