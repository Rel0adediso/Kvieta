using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Otium.App.Services;

public static class LocalCompanionSiteContent
{
    private static readonly Lazy<string> Script = new(LoadScript);

    public static string CreateHtml(string apiUrl)
    {
        string config = JsonSerializer.Serialize(new { apiUrl });
        return $$"""
            <!doctype html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
              <meta name="color-scheme" content="dark">
              <meta name="theme-color" content="#171813">
              <title>Otium · Yönetici cihazı</title>
              <style>
                :root{font-family:"Segoe UI Variable Text","Segoe UI",system-ui,sans-serif;color:#f1eee3;background:#171813;color-scheme:dark}
                *{box-sizing:border-box}body{margin:0;min-height:100svh;background:#171813}
                body::before{content:"";position:fixed;inset:0 0 auto;height:3px;background:#b4bc82}
                main{width:min(100%,500px);min-height:100svh;margin:auto;padding:max(28px,env(safe-area-inset-top)) 18px max(24px,env(safe-area-inset-bottom));display:grid;place-items:center}
                .card{width:100%;border:1px solid #37392f;border-radius:14px;padding:22px;background:#282920;box-shadow:0 18px 50px #0e0f0b80}
                .brand{display:flex;align-items:center;gap:10px;color:#f1eee3;font-size:12px;font-weight:650}.brand::after{content:"Yerel yönetici onayı";margin-left:auto;color:#949184;font-size:10px;font-weight:500}.mark{position:relative;width:29px;height:29px;border-radius:8px;background:#b4bc82}.mark::after{content:"O";position:absolute;inset:0;display:grid;place-items:center;color:#202217;font-size:14px;font-weight:750}
                h1{font-family:"Segoe UI Variable Display","Segoe UI",system-ui,sans-serif;font-size:clamp(24px,7vw,31px);line-height:1.12;margin:24px 0 8px;letter-spacing:-.025em;font-weight:650}p{margin:0;color:#b0ac9f;font-size:14px;line-height:1.55}
                .compare{margin:24px 0 16px;padding:17px;border:1px solid #45483a;border-radius:10px;background:#1f201a;text-align:center}.compare small{display:block;color:#949184;font-size:10px;font-weight:600;letter-spacing:.055em;text-transform:uppercase}.code{margin-top:7px;color:#b4bc82;font-family:"Segoe UI Variable Display","Segoe UI",system-ui,sans-serif;font-size:37px;font-weight:650;letter-spacing:.15em;font-variant-numeric:tabular-nums}
                .notice{display:flex;gap:10px;margin:0 0 18px;padding:12px;border:1px solid #37392f;border-radius:9px;background:#1b1c17;color:#b0ac9f;font-size:12px;line-height:1.45}.notice::before{content:"i";flex:0 0 20px;height:20px;border:1px solid #707650;border-radius:50%;display:grid;place-items:center;color:#b4bc82;font-weight:700}
                .actions{display:grid;gap:9px}button{min-height:46px;border-radius:7px;padding:0 16px;font:inherit;font-weight:650;cursor:pointer}button:disabled{opacity:.42;cursor:default}.primary{border:0;color:#202217;background:#b4bc82}.primary:hover{background:#c7ce96}.primary:active{opacity:.82}.secondary{color:#f1eee3;background:#282a23;border:1px solid #37392f}.secondary:hover{background:#303229}
                .result{margin-top:14px;padding:11px 12px;border-radius:8px;font-size:12px;line-height:1.45}.result:empty{display:none}.success{color:#9abc80;background:#293523}.error{color:#d98080;background:#382322}.loading{text-align:center;color:#b4bc82;font-size:13px}.loading.error{color:#d98080}
                @media(prefers-color-scheme:light){:root{color:#22231d;background:#d8d6cc;color-scheme:light}body{background:#d8d6cc}body::before{background:#536039}.card{color:#22231d;background:#f2eee3;border-color:#bdbcb0;box-shadow:0 18px 45px #64625830}.brand{color:#22231d}.brand::after{color:#606257}.mark{background:#536039}.mark::after{color:#f6f5ec}p{color:#505248}.compare{background:#eae6dc;border-color:#aaa99c}.compare small{color:#606257}.code{color:#536039}.notice{background:#e3dfd5;border-color:#bdbcb0;color:#505248}.notice::before{border-color:#879069;color:#536039}.primary{color:#f6f5ec;background:#536039}.primary:hover{background:#46522f}.secondary{color:#22231d;background:#dedbd1;border-color:#bdbcb0}.secondary:hover{background:#d2cfc4}.success{color:#4e6b3e;background:#cfdcc7}.error{color:#973f3f;background:#ead0cd}.loading{color:#536039}.loading.error{color:#973f3f} }
                [hidden]{display:none!important}
              </style>
            </head>
            <body>
              <main>
                <div id="loading" class="loading">Otium bilgisayarına bağlanıyor…</div>
                <section id="app" class="card" hidden>
                  <div class="brand"><span class="mark"></span>Otium Companion</div>
                  <h1 id="title"></h1>
                  <p id="description"></p>
                  <div class="compare"><small>Bilgisayardaki kodla karşılaştır</small><div id="code" class="code"></div></div>
                  <div class="notice">Yalnız aynı yerel ağda ve kodlar birebir eşleşiyorsa devam et. Anahtar bu tarayıcı profilinde kalır.</div>
                  <div class="actions"><button id="approve" class="primary"></button><button id="reject" class="secondary">Reddet</button></div>
                  <div id="result" class="result"></div>
                </section>
              </main>
              <script>window.__OTIUM_CONFIG__={{config}};</script>
              <script>{{Script.Value}}</script>
            </body>
            </html>
            """;
    }

    private static string LoadScript()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Otium.Companion.js")
            ?? throw new InvalidOperationException("Embedded companion site bundle is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
