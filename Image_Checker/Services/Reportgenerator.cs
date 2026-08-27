using Image_Checker.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Image_Checker.Services
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  Input DTOs
    // ─────────────────────────────────────────────────────────────────────────────

    public class DatasetStats
    {
        /// <summary>Total images in dataset (after crop/filter).</summary>
        public int TotalImages { get; set; }

        /// <summary>Per-class image counts. Key = class name, Value = count.</summary>
        public Dictionary<string, int> ClassCounts { get; set; } = new();

        /// <summary>Image format string, e.g. "Grayscale 8bpp" or "RGB 24bpp".</summary>
        public string ImageFormat { get; set; }

        /// <summary>Image dimensions used during training.</summary>
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        /// <summary>Train count (derived from 80/20 split).</summary>
        public int TrainCount { get; set; }

        /// <summary>Test count.</summary>
        public int TestCount { get; set; }
    }

    public class PipelineConfig
    {
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public bool IsGrayscale { get; set; }
        public int PcaRank { get; set; }
        public bool AbsolutePaths { get; set; }
        public int ClassCount { get; set; }
    }

    public class TrialRecord
    {
        public int TrialNumber { get; set; }
        public Dictionary<string, object> Params { get; set; } = new();
        public double Score { get; set; }       // NaN or negative infinity if failed
        public bool Failed { get; set; }
        public bool Cancelled { get; set; }
    }

    public class TunerRunResult
    {
        public string TunerName { get; set; }   // "FastTree" or "LightGBM"
        public List<TrialRecord> Trials { get; set; } = new();
        public IDictionary<string, object> BestParams { get; set; } = new Dictionary<string, object>();
        public double BestScore { get; set; }
        public int CVFolds { get; set; }
        public double SampleFraction { get; set; }
    }

    public class ReportModelResult
    {
        public string Name { get; set; }
        public double MacroAccuracy { get; set; }
        public double MicroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public double TrainTimeSeconds { get; set; }
    }

    public class ReportInput
    {
        public DatasetStats Dataset { get; set; }
        public PipelineConfig Pipeline { get; set; }
        public List<TunerRunResult> TunerResults { get; set; } = new();
        public List<ReportModelResult> ModelResults { get; set; } = new();
        public ConfusionMatrixResult ConfusionMatrix { get; set; }
        public string BestModelName { get; set; }
        public string ModelZipPath { get; set; }
        public List<string> ArtifactPaths { get; set; } = new();
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public int CVFolds { get; set; }
        public int TuningTrials { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Generator
    // ─────────────────────────────────────────────────────────────────────────────

    public static class ReportGenerator
    {
        /// <summary>
        /// Writes a self-contained HTML training report next to the .zip model file.
        /// Returns the path of the written file.
        /// </summary>
        public static string Write(ReportInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var reportPath = string.IsNullOrWhiteSpace(input.ModelZipPath)
                ? Path.Combine(input.OutputPath ?? ".", $"training_report_{DateTime.Now:yyyyMMddHHmmss}.html")
                : Path.ChangeExtension(input.ModelZipPath, ".report.html");

            var html = BuildHtml(input);
            File.WriteAllText(reportPath, html, Encoding.UTF8);
            Console.WriteLine($"\n📄 Training report saved: {Path.GetFileName(reportPath)}");
            Console.WriteLine($"   Location: {reportPath}");
            return reportPath;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HTML builder
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildHtml(ReportInput r)
        {
            var best = r.ModelResults?
                .OrderByDescending(m => m.MacroAccuracy)
                .FirstOrDefault();

            var duration = r.SessionEnd - r.SessionStart;
            string durationStr = duration.TotalMinutes >= 60
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"~{(int)duration.TotalMinutes} min";

            var sb = new StringBuilder(65536);

            // ── HEAD ──────────────────────────────────────────────────────────
            sb.Append(@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>ML Training Report");
            if (best != null) sb.Append($" — {best.Name}");
            sb.Append(@"</title>
<style>
@import url('https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:ital,wght@0,300;0,400;0,500;0,600;0,700;1,400&family=IBM+Plex+Mono:wght@400;500;600&family=IBM+Plex+Sans+Condensed:wght@600;700&display=swap');

:root {
  --bg:        #0a0d0f;
  --bg1:       #0f1417;
  --bg2:       #141c22;
  --bg3:       #1a2530;
  --line:      #1f303d;
  --line2:     #273d4e;
  --text:      #d4e6f1;
  --text2:     #7ca0b4;
  --text3:     #4a6b7c;
  --accent:    #0f62fe;
  --accentlt:  #4589ff;
  --teal:      #08bdba;
  --green:     #24a148;
  --greenlt:   #42be65;
  --red:       #da1e28;
  --redlt:     #ff8389;
  --yellow:    #f1c21b;
  --orange:    #ff832b;
  --white:     #ffffff;
  --plex:      'IBM Plex Sans', sans-serif;
  --cond:      'IBM Plex Sans Condensed', sans-serif;
  --mono:      'IBM Plex Mono', monospace;
}

*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
html{scroll-behavior:smooth}
body{background:var(--bg);color:var(--text);font-family:var(--plex);font-size:14px;line-height:1.6;-webkit-font-smoothing:antialiased}
body::before{content:'';position:fixed;inset:0;background-image:url(""data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)' opacity='0.04'/%3E%3C/svg%3E"");pointer-events:none;z-index:0;opacity:.6}
.top-rule{height:3px;background:linear-gradient(90deg,var(--accent) 0%,var(--teal) 50%,var(--greenlt) 100%)}
.page{position:relative;z-index:1;max-width:1080px;margin:0 auto;padding:48px 32px 96px}

/* HEADER */
.header{margin-bottom:56px;animation:reveal .5s ease both}
.header-eyebrow{display:flex;align-items:center;gap:20px;margin-bottom:20px}
.status-pill{display:inline-flex;align-items:center;gap:7px;background:rgba(36,161,72,.12);border:1px solid rgba(36,161,72,.3);color:var(--greenlt);font-family:var(--mono);font-size:11px;font-weight:500;letter-spacing:.08em;padding:4px 12px}
.status-pill::before{content:'';width:6px;height:6px;border-radius:50%;background:var(--greenlt);box-shadow:0 0 8px var(--greenlt);animation:blink 2s ease infinite}
.header-time{font-family:var(--mono);font-size:11px;color:var(--text3);letter-spacing:.06em}
.header-title{font-family:var(--cond);font-size:clamp(34px,5.5vw,62px);font-weight:700;line-height:1;letter-spacing:-.01em;color:var(--white);margin-bottom:10px}
.header-title em{font-style:normal;color:var(--accentlt)}
.header-subtitle{font-size:14px;color:var(--text2);font-weight:300;display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.header-subtitle .sep{color:var(--line2)}
.meta-row{margin-top:28px;display:flex;gap:0;border:1px solid var(--line);overflow:hidden;flex-wrap:wrap}
.meta-cell{flex:1;min-width:120px;padding:14px 20px;border-right:1px solid var(--line)}
.meta-cell:last-child{border-right:none}
.meta-label{font-family:var(--mono);font-size:10px;letter-spacing:.12em;color:var(--text3);text-transform:uppercase;margin-bottom:4px}
.meta-value{font-family:var(--mono);font-size:13px;color:var(--text);font-weight:500}

/* SECTION */
.section{margin-bottom:52px}
.section-head{display:flex;align-items:center;gap:12px;margin-bottom:20px}
.section-num{font-family:var(--mono);font-size:11px;color:var(--accent);letter-spacing:.1em;font-weight:500;min-width:32px}
.section-title{font-family:var(--cond);font-size:13px;font-weight:600;letter-spacing:.14em;text-transform:uppercase;color:var(--text2)}
.section-head::after{content:'';flex:1;height:1px;background:var(--line)}

/* DATASET GRID */
.dataset-grid{display:grid;grid-template-columns:repeat(5,1fr);gap:1px;background:var(--line);border:1px solid var(--line);margin-bottom:20px;animation:reveal .5s .05s ease both}
@media(max-width:740px){.dataset-grid{grid-template-columns:repeat(2,1fr)}}
.ds-card{background:var(--bg1);padding:22px 20px;transition:background .2s}
.ds-card:hover{background:var(--bg2)}
.ds-label{font-family:var(--mono);font-size:10px;letter-spacing:.12em;color:var(--text3);text-transform:uppercase;margin-bottom:10px}
.ds-value{font-family:var(--cond);font-size:30px;font-weight:700;line-height:1;margin-bottom:4px}
.ds-sub{font-size:11px;color:var(--text3);font-family:var(--mono)}
.c-total{color:var(--accentlt)} .c-ok{color:var(--greenlt)} .c-ng{color:var(--redlt)} .c-fmt{color:var(--orange)} .c-split{color:var(--teal)}
.dist-bar-wrap{border:1px solid var(--line);overflow:hidden;height:36px;display:flex;animation:reveal .5s .1s ease both}
.dist-bar-legend{display:flex;gap:24px;margin-top:8px;font-family:var(--mono);font-size:11px;color:var(--text3)}
.dist-bar-legend span{display:flex;align-items:center;gap:6px}
.ldot{width:8px;height:8px;flex-shrink:0}

/* PIPELINE */
.pipeline{display:flex;align-items:stretch;border:1px solid var(--line);overflow:hidden;animation:reveal .5s .1s ease both}
@media(max-width:680px){.pipeline{flex-direction:column}}
.pipe-step{flex:1;background:var(--bg1);padding:16px 14px;border-right:1px solid var(--line);transition:background .2s}
.pipe-step:last-child{border-right:none}
.pipe-step:hover{background:var(--bg2)}
.pipe-num{font-family:var(--mono);font-size:9px;color:var(--accent);letter-spacing:.1em;margin-bottom:6px}
.pipe-name{font-size:12px;font-weight:600;color:var(--text);margin-bottom:4px;line-height:1.3}
.pipe-detail{font-family:var(--mono);font-size:10px;color:var(--text3);line-height:1.5}

/* TUNING */
.tuning-grid{display:grid;grid-template-columns:1fr 1fr;gap:16px;animation:reveal .5s .15s ease both}
@media(max-width:680px){.tuning-grid{grid-template-columns:1fr}}
.tuner-card{border:1px solid var(--line);overflow:hidden}
.tuner-header{background:var(--bg2);border-bottom:1px solid var(--line);padding:14px 18px;display:flex;justify-content:space-between;align-items:center}
.tuner-name{font-family:var(--cond);font-size:15px;font-weight:700;color:var(--white);letter-spacing:.02em}
.tuner-badge{font-family:var(--mono);font-size:11px;color:var(--yellow);background:rgba(241,194,27,.1);border:1px solid rgba(241,194,27,.25);padding:3px 10px;letter-spacing:.06em}
.trials-scroll{max-height:260px;overflow-y:auto;scrollbar-width:thin;scrollbar-color:var(--line2) transparent}
.trial{display:flex;align-items:center;gap:10px;padding:7px 18px;border-bottom:1px solid rgba(31,48,61,.6);transition:background .15s}
.trial:hover{background:rgba(15,98,254,.04)}
.trial.best{background:rgba(241,194,27,.05)}
.trial.poor{background:rgba(218,30,40,.05)}
.t-num{font-family:var(--mono);font-size:10px;color:var(--text3);width:18px;flex-shrink:0}
.t-bar{flex:1;height:4px;background:var(--bg3);overflow:hidden}
.t-fill{height:100%;background:var(--accent);transition:width .8s cubic-bezier(.16,1,.3,1)}
.trial.best .t-fill{background:var(--yellow)}
.trial.poor .t-fill{background:var(--red)}
.t-score{font-family:var(--mono);font-size:11px;color:var(--text);width:52px;text-align:right;flex-shrink:0}
.trial.best .t-score{color:var(--yellow);font-weight:600}
.trial.poor .t-score{color:var(--redlt)}
.t-flag{width:14px;font-size:10px;flex-shrink:0}
.tuner-params{background:var(--bg2);border-top:1px solid var(--line);padding:10px 18px;display:flex;flex-wrap:wrap;gap:6px}
.param{font-family:var(--mono);font-size:10px;color:var(--teal);background:rgba(8,189,186,.07);border:1px solid rgba(8,189,186,.18);padding:2px 9px;letter-spacing:.04em}

/* MODEL TABLE */
.model-table{border:1px solid var(--line);overflow:hidden;animation:reveal .5s .2s ease both}
.model-row{display:grid;grid-template-columns:220px 1fr 1fr 1fr 52px;border-bottom:1px solid var(--line);align-items:stretch;transition:background .15s}
.model-row:last-child{border-bottom:none}
.model-row:hover{background:rgba(15,98,254,.03)}
.model-row.winner{background:rgba(241,194,27,.04);border-left:2px solid var(--yellow)}
.model-row.winner:hover{background:rgba(241,194,27,.07)}
@media(max-width:760px){.model-row{grid-template-columns:1fr 1fr;grid-template-rows:auto auto}.m-name{grid-column:1/-1}.m-rank{display:none}}
.m-cell{padding:18px 20px;border-right:1px solid var(--line)}
.m-cell:last-child{border-right:none}
.m-name{display:flex;flex-direction:column;justify-content:center;gap:6px}
.m-name-text{font-family:var(--mono);font-size:13px;font-weight:600;color:var(--white)}
.winner-tag{display:inline-block;font-family:var(--mono);font-size:9px;font-weight:600;letter-spacing:.12em;color:#000;background:var(--yellow);padding:2px 8px}
.m-metric{display:flex;flex-direction:column;justify-content:center}
.m-metric-label{font-family:var(--mono);font-size:9px;letter-spacing:.12em;color:var(--text3);text-transform:uppercase;margin-bottom:5px}
.m-metric-value{font-family:var(--cond);font-size:22px;font-weight:700;line-height:1;margin-bottom:6px}
.v-win{color:var(--yellow)} .v-good{color:var(--accentlt)} .v-ok{color:var(--greenlt)} .v-dim{color:var(--text2)}
.m-bar{height:3px;background:var(--bg3);overflow:hidden}
.m-bar-fill{height:100%;background:var(--accent);transition:width 1s cubic-bezier(.16,1,.3,1)}
.winner .m-bar-fill{background:var(--yellow)}
.m-rank{display:flex;align-items:center;justify-content:center}
.rank-circle{width:30px;height:30px;border-radius:50%;border:1px solid var(--line2);display:flex;align-items:center;justify-content:center;font-family:var(--cond);font-weight:700;font-size:14px;color:var(--text3)}
.rank-circle.r1{border-color:var(--yellow);color:var(--yellow);background:rgba(241,194,27,.1)}

/* WINNER CALLOUT */
.winner-callout{border:1px solid var(--yellow);background:linear-gradient(135deg,rgba(241,194,27,.06) 0%,rgba(15,98,254,.03) 100%);padding:32px 36px;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:24px;position:relative;overflow:hidden;animation:reveal .5s .25s ease both}
.winner-callout::after{content:'01';position:absolute;right:-8px;top:-20px;font-family:var(--cond);font-size:160px;font-weight:700;color:var(--yellow);opacity:.04;line-height:1;pointer-events:none;user-select:none}
.wc-left h3{font-family:var(--mono);font-size:10px;letter-spacing:.16em;color:var(--yellow);text-transform:uppercase;margin-bottom:8px}
.wc-left h2{font-family:var(--cond);font-size:clamp(22px,3vw,34px);font-weight:700;color:var(--white);margin-bottom:8px}
.wc-left p{font-size:13px;color:var(--text2);font-weight:300}
.wc-metrics{display:flex;gap:32px;flex-wrap:wrap}
.wc-metric{text-align:right}
.wc-metric-val{font-family:var(--cond);font-size:clamp(26px,3.5vw,40px);font-weight:700;color:var(--yellow);line-height:1;margin-bottom:4px}
.wc-metric-label{font-family:var(--mono);font-size:10px;letter-spacing:.1em;color:var(--text3);text-transform:uppercase}

/* CONFUSION MATRIX */
.cm-wrap{animation:reveal .5s .3s ease both}
.cm-table-outer{border:1px solid var(--line);overflow-x:auto}
.cm-table{width:100%;border-collapse:collapse;min-width:360px}
.cm-table th,.cm-table td{padding:14px 18px;border:1px solid var(--line);font-family:var(--mono);font-size:12px}
.cm-table thead th{background:var(--bg2);color:var(--text3);font-size:10px;letter-spacing:.12em;text-transform:uppercase}
.cm-table .cm-row-label{background:var(--bg1);color:var(--text2);font-weight:500;text-align:left}
.cm-hit{background:rgba(36,161,72,.12);color:var(--greenlt);font-weight:600;font-size:16px}
.cm-miss{background:rgba(218,30,40,.08);color:var(--redlt)}
.per-class{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:12px;margin-top:16px}
.pc-card{border:1px solid var(--line);padding:18px 20px;background:var(--bg1)}
.pc-label-row{display:flex;align-items:center;justify-content:space-between;margin-bottom:14px}
.pc-label-name{font-family:var(--cond);font-size:18px;font-weight:700;color:var(--white)}
.pc-badge{font-family:var(--mono);font-size:10px;padding:2px 8px;letter-spacing:.08em}
.badge-ok{background:rgba(36,161,72,.12);border:1px solid rgba(36,161,72,.3);color:var(--greenlt)}
.badge-ng{background:rgba(218,30,40,.12);border:1px solid rgba(218,30,40,.3);color:var(--redlt)}
.badge-cls{background:rgba(69,137,255,.12);border:1px solid rgba(69,137,255,.3);color:var(--accentlt)}
.pc-metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}
.pc-metric-label{font-family:var(--mono);font-size:9px;letter-spacing:.1em;color:var(--text3);text-transform:uppercase;margin-bottom:4px}
.pc-metric-val{font-family:var(--cond);font-size:20px;font-weight:700}
.pc-bar{height:3px;background:var(--bg3);margin-top:5px;overflow:hidden}
.pc-fill{height:100%;transition:width 1s ease}
.fill-ok{background:var(--greenlt)} .fill-ng{background:var(--redlt)} .fill-cls{background:var(--accentlt)}

/* OUTPUT FILES */
.output-files{border:1px solid var(--line);overflow:hidden;animation:reveal .5s .35s ease both}
.file-row{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--line);gap:16px;flex-wrap:wrap;transition:background .15s}
.file-row:last-child{border-bottom:none}
.file-row:hover{background:var(--bg2)}
.file-left{display:flex;align-items:center;gap:14px}
.file-icon{width:38px;height:38px;background:var(--bg3);border:1px solid var(--line2);display:flex;align-items:center;justify-content:center;font-size:16px;flex-shrink:0}
.file-name{font-family:var(--mono);font-size:13px;color:var(--teal);margin-bottom:3px}
.file-path{font-family:var(--mono);font-size:10px;color:var(--text3)}
.file-right{display:flex;gap:6px;flex-wrap:wrap}
.ftag{font-family:var(--mono);font-size:9px;letter-spacing:.1em;text-transform:uppercase;padding:3px 10px;border:1px solid}
.ftag-zip{color:var(--teal);border-color:rgba(8,189,186,.3);background:rgba(8,189,186,.07)}
.ftag-json{color:var(--orange);border-color:rgba(255,131,43,.3);background:rgba(255,131,43,.07)}
.ftag-html{color:var(--accentlt);border-color:rgba(69,137,255,.3);background:rgba(69,137,255,.07)}
.ftag-csv{color:var(--greenlt);border-color:rgba(66,190,101,.3);background:rgba(66,190,101,.07)}

/* FOOTER */
.footer{margin-top:64px;padding-top:20px;border-top:1px solid var(--line);display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px}
.footer-text{font-family:var(--mono);font-size:11px;color:var(--text3);letter-spacing:.06em}

/* ANIMATIONS */
@keyframes reveal{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:translateY(0)}}
@keyframes blink{0%,100%{opacity:1}50%{opacity:.3}}
@keyframes growWidth{from{transform:scaleX(0)}to{transform:scaleX(1)}}
</style>
</head>
<body>
<div class=""top-rule""></div>
<div class=""page"">
");

            // ── HEADER ────────────────────────────────────────────────────────
            var classNames = r.Dataset?.ClassCounts?.Keys.ToList() ?? new List<string>();
            int modelCount = r.ModelResults?.Count ?? 0;
            string imgDesc = r.Pipeline != null
                ? $"{(r.Pipeline.IsGrayscale ? "Grayscale" : "RGB")} · {r.Pipeline.ImageWidth}×{r.Pipeline.ImageHeight}px"
                : "";

            sb.Append($@"
  <header class=""header"">
    <div class=""header-eyebrow"">
      <div class=""status-pill"">TRAINING COMPLETE</div>
      <div class=""header-time"">Session {r.SessionStart:yyyy-MM-dd} · {r.SessionStart:HH:mm:ss} → {r.SessionEnd:HH:mm:ss}</div>
    </div>
    <h1 class=""header-title"">ML.NET<br><em>Training Report</em></h1>
    <p class=""header-subtitle"">
      <span>{(r.Dataset?.ClassCounts?.Count ?? 0)}-Class Classification</span>
      <span class=""sep"">·</span>
      <span>{modelCount} Models Evaluated</span>
      <span class=""sep"">·</span>
      <span>{imgDesc}</span>
    </p>
    <div class=""meta-row"">
      <div class=""meta-cell""><div class=""meta-label"">Source Path</div><div class=""meta-value"">{Esc(r.SourcePath ?? "-")}</div></div>
      <div class=""meta-cell""><div class=""meta-label"">Output Path</div><div class=""meta-value"">{Esc(r.OutputPath ?? "-")}</div></div>
      <div class=""meta-cell""><div class=""meta-label"">Duration</div><div class=""meta-value"">{durationStr}</div></div>
      <div class=""meta-cell""><div class=""meta-label"">CV Folds</div><div class=""meta-value"">{r.CVFolds}-fold</div></div>
      <div class=""meta-cell""><div class=""meta-label"">Tuning Trials</div><div class=""meta-value"">{r.TuningTrials} each</div></div>
    </div>
  </header>
");

            // ── 01 DATASET ────────────────────────────────────────────────────
            sb.Append(@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">01</span><span class=""section-title"">Dataset Overview</span></div>
    <div class=""dataset-grid"">
");
            int total = r.Dataset?.TotalImages ?? 0;
            sb.Append($@"      <div class=""ds-card""><div class=""ds-label"">Total Images</div><div class=""ds-value c-total"">{total:N0}</div><div class=""ds-sub"">After ROI crop</div></div>
");

            // Dynamic per-class cards (up to first 2 shown specially, rest generic)
            var classColors = new[] { "c-ok", "c-ng", "c-split", "c-fmt", "c-total" };
            var classBadges = new[] { "PASS CLASS", "DEFECT CLASS", "CLASS", "CLASS", "CLASS" };
            int ci = 0;
            if (r.Dataset?.ClassCounts != null)
            {
                foreach (var kv in r.Dataset.ClassCounts)
                {
                    double pct = total > 0 ? kv.Value * 100.0 / total : 0;
                    string cc = classColors[Math.Min(ci, classColors.Length - 1)];
                    sb.Append($@"      <div class=""ds-card""><div class=""ds-label"">{Esc(kv.Key)} Class</div><div class=""ds-value {cc}"">{kv.Value:N0}</div><div class=""ds-sub"">{pct:F2}% of set</div></div>
");
                    ci++;
                }
            }

            string fmtShort = r.Dataset?.ImageFormat ?? "-";
            sb.Append($@"      <div class=""ds-card""><div class=""ds-label"">Image Format</div><div class=""ds-value c-fmt"" style=""font-size:18px;padding-top:6px"">{Esc(fmtShort)}</div><div class=""ds-sub"">{(r.Pipeline?.IsGrayscale == true ? "No TL" : "TL OK")}</div></div>
      <div class=""ds-card""><div class=""ds-label"">Train / Test</div><div class=""ds-value c-split"" style=""font-size:18px;padding-top:6px"">80 / 20</div><div class=""ds-sub"">{r.Dataset?.TrainCount ?? 0} · {r.Dataset?.TestCount ?? 0}</div></div>
    </div>
");

            // Distribution bar — first 2 classes
            if (r.Dataset?.ClassCounts != null && r.Dataset.ClassCounts.Count >= 2)
            {
                var kvList = r.Dataset.ClassCounts.ToList();
                double pct0 = total > 0 ? kvList[0].Value * 100.0 / total : 50;
                string cls0 = kvList[0].Key; int cnt0 = kvList[0].Value;
                string cls1 = kvList[1].Key; int cnt1 = kvList[1].Value;
                sb.Append($@"    <div class=""dist-bar-wrap"">
      <div style=""background:linear-gradient(90deg,#145523,#24a148);height:100%;width:{pct0:F2}%;display:flex;align-items:center;padding:0 16px;font-family:var(--mono);font-size:11px;font-weight:600;color:#b7f0c8;letter-spacing:.06em;transform-origin:left;animation:growWidth 1.2s .3s cubic-bezier(.16,1,.3,1) both"">{Esc(cls0)} — {cnt0:N0}</div>
      <div style=""background:linear-gradient(90deg,#520007,#da1e28);flex:1;height:100%;display:flex;align-items:center;justify-content:center;font-family:var(--mono);font-size:10px;color:#ffb3b8;font-weight:600"">{Esc(cls1)}</div>
    </div>
    <div class=""dist-bar-legend"">
      <span><svg class=""ldot"" viewBox=""0 0 8 8""><rect width=""8"" height=""8"" fill=""#24a148""/></svg>{Esc(cls0)} · {cnt0:N0} · {pct0:F2}%</span>
      <span><svg class=""ldot"" viewBox=""0 0 8 8""><rect width=""8"" height=""8"" fill=""#da1e28""/></svg>{Esc(cls1)} · {cnt1:N0} · {(100 - pct0):F2}%</span>
    </div>
");
            }
            sb.Append("  </section>\n");

            // ── 02 PIPELINE ───────────────────────────────────────────────────
            var pip = r.Pipeline;
            sb.Append(@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">02</span><span class=""section-title"">Preprocessing Pipeline</span></div>
    <div class=""pipeline"">
");
            string[] stepNames = { "Load Images", "Resize", "Extract Pixels", "PCA", "Label Encode" };
            string[] stepDetails =
            {
                $"LoadImages\n{(pip?.AbsolutePaths == true ? "Absolute paths" : "Relative paths")}",
                $"{pip?.ImageWidth ?? 0} × {pip?.ImageHeight ?? 0} px\nIsoCrop",
                pip?.IsGrayscale == true ? "Grayscale norm\n÷255, offset 0" : "RGB interleaved\noffset 128, ÷128",
                $"Rank {pip?.PcaRank ?? 0}\nRawFeatures→Features",
                $"MapValueToKey\n{pip?.ClassCount ?? 0} classes"
            };
            for (int s = 0; s < stepNames.Length; s++)
            {
                string detail = stepDetails[s].Replace("\n", "<br>");
                sb.Append($@"      <div class=""pipe-step""><div class=""pipe-num"">STEP {s + 1:D2}</div><div class=""pipe-name"">{stepNames[s]}</div><div class=""pipe-detail"">{detail}</div></div>
");
            }
            sb.Append("    </div>\n  </section>\n");

            // ── 03 TUNING ─────────────────────────────────────────────────────
            if (r.TunerResults?.Any() == true)
            {
                sb.Append($@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">03</span><span class=""section-title"">Hyperparameter Tuning — {(int)(r.TunerResults.FirstOrDefault()?.SampleFraction * 100 ?? 50)}% Sample · {r.CVFolds}-Fold CV</span></div>
    <div class=""tuning-grid"">
");
                foreach (var tuner in r.TunerResults)
                {
                    string bestScoreStr = double.IsNegativeInfinity(tuner.BestScore) ? "N/A"
                        : $"{tuner.BestScore:P2}";

                    sb.Append($@"      <div class=""tuner-card"">
        <div class=""tuner-header"">
          <div class=""tuner-name"">{Esc(tuner.TunerName)}</div>
          <div class=""tuner-badge"">Best {bestScoreStr}</div>
        </div>
        <div class=""trials-scroll"">
");
                    double maxScore = tuner.Trials.Where(t => !t.Failed && !t.Cancelled && !double.IsNaN(t.Score))
                        .Select(t => t.Score).DefaultIfEmpty(1).Max();
                    double overallBest = tuner.BestScore;

                    foreach (var trial in tuner.Trials)
                    {
                        string cls = "";
                        string scoreLabel;
                        double fillPct = 0;
                        string flag = "";

                        if (trial.Cancelled)
                        {
                            scoreLabel = "CANCEL";
                            cls = "poor";
                        }
                        else if (trial.Failed || double.IsNaN(trial.Score))
                        {
                            scoreLabel = "FAILED";
                            cls = "poor";
                            flag = "!";
                        }
                        else
                        {
                            scoreLabel = $"{trial.Score:P2}";
                            fillPct = maxScore > 0 ? trial.Score / maxScore * 100.0 : 0;
                            if (Math.Abs(trial.Score - overallBest) < 1e-9)
                            {
                                cls = "best";
                                flag = "★";
                            }
                        }

                        sb.Append($@"          <div class=""trial {cls}"">
            <span class=""t-num"">{trial.TrialNumber:D2}</span>
            <div class=""t-bar""><div class=""t-fill"" style=""width:{fillPct:F1}%""></div></div>
            <span class=""t-score"">{scoreLabel}</span>
            <span class=""t-flag"">{flag}</span>
          </div>
");
                    }

                    sb.Append("        </div>\n        <div class=\"tuner-params\">\n");
                    foreach (var kv in tuner.BestParams)
                        sb.Append($"          <span class=\"param\">{Esc(kv.Key)}: {Esc(kv.Value?.ToString())}</span>\n");
                    sb.Append("        </div>\n      </div>\n");
                }
                sb.Append("    </div>\n  </section>\n");
            }

            // ── 04 MODEL COMPARISON ───────────────────────────────────────────
            if (r.ModelResults?.Any() == true)
            {
                var sorted = r.ModelResults.OrderByDescending(m => m.MacroAccuracy).ToList();
                double maxMacro = sorted.First().MacroAccuracy;

                sb.Append(@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">04</span><span class=""section-title"">Final Model Evaluation — Test Set</span></div>
    <div class=""model-table"">
");
                int rank = 1;
                foreach (var m in sorted)
                {
                    bool isWinner = m.Name == r.BestModelName || (rank == 1);
                    string rowCls = isWinner ? " winner" : "";
                    string vMac = isWinner ? "v-win" : (rank <= 2 ? "v-good" : "v-dim");
                    string vMic = isWinner ? "v-win" : (rank <= 2 ? "v-good" : "v-dim");
                    string vLog = isWinner ? "v-win" : "v-ok";
                    string rankCls = rank == 1 ? " r1" : "";
                    double macPct = maxMacro > 0 ? m.MacroAccuracy / maxMacro * 100.0 : 0;

                    sb.Append($@"      <div class=""model-row{rowCls}"">
        <div class=""m-cell m-name"">
          <div class=""m-name-text"">{Esc(m.Name)}</div>
          {(isWinner ? "<span class=\"winner-tag\">BEST MODEL</span>" : "")}
        </div>
        <div class=""m-cell m-metric"">
          <div class=""m-metric-label"">Macro Accuracy</div>
          <div class=""m-metric-value {vMac}"">{m.MacroAccuracy:P2}</div>
          <div class=""m-bar""><div class=""m-bar-fill"" style=""width:{m.MacroAccuracy * 100:F2}%""></div></div>
        </div>
        <div class=""m-cell m-metric"">
          <div class=""m-metric-label"">Micro Accuracy</div>
          <div class=""m-metric-value {vMic}"">{m.MicroAccuracy:P2}</div>
          <div class=""m-bar""><div class=""m-bar-fill"" style=""width:{m.MicroAccuracy * 100:F2}%""></div></div>
        </div>
        <div class=""m-cell m-metric"">
          <div class=""m-metric-label"">Log Loss</div>
          <div class=""m-metric-value {vLog}"">{m.LogLoss:F4}</div>
          <div class=""m-bar""><div class=""m-bar-fill"" style=""width:{Math.Min(m.LogLoss * 500, 100):F1}%; background:var(--greenlt)""></div></div>
        </div>
        <div class=""m-cell m-rank""><div class=""rank-circle{rankCls}"">{rank}</div></div>
      </div>
");
                    rank++;
                }
                sb.Append("    </div>\n  </section>\n");
            }

            // ── 05 WINNER CALLOUT ─────────────────────────────────────────────
            if (best != null)
            {
                string classesStr = string.Join(", ", classNames.Select(c => $"<strong>{Esc(c)}</strong>"));
                sb.Append($@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">05</span><span class=""section-title"">Selected Model</span></div>
    <div class=""winner-callout"">
      <div class=""wc-left"">
        <h3>🏆 Best Model by Macro Accuracy</h3>
        <h2>{Esc(best.Name)}</h2>
        <p>Multiclass Classification · Classifies: {classesStr}</p>
      </div>
      <div class=""wc-metrics"">
        <div class=""wc-metric""><div class=""wc-metric-val"">{best.MacroAccuracy:P2}</div><div class=""wc-metric-label"">Macro Acc</div></div>
        <div class=""wc-metric""><div class=""wc-metric-val"">{best.MicroAccuracy:P2}</div><div class=""wc-metric-label"">Micro Acc</div></div>
        <div class=""wc-metric""><div class=""wc-metric-val"">{best.LogLoss:F4}</div><div class=""wc-metric-label"">Log Loss</div></div>
        <div class=""wc-metric""><div class=""wc-metric-val"">{best.TrainTimeSeconds:F0}s</div><div class=""wc-metric-label"">Train Time</div></div>
      </div>
    </div>
  </section>
");
            }

            // ── 06 CONFUSION MATRIX ───────────────────────────────────────────
            var cm = r.ConfusionMatrix;
            if (cm != null && cm.Labels?.Count > 0)
            {
                int n = cm.Labels.Count;
                sb.Append(@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">06</span><span class=""section-title"">Confusion Matrix &amp; Per-Class Metrics</span></div>
    <div class=""cm-wrap"">
      <div class=""cm-table-outer"">
        <table class=""cm-table"">
          <thead>
            <tr>
              <th style=""text-align:left"">Actual \ Predicted</th>
");
                foreach (var lbl in cm.Labels)
                    sb.Append($"              <th>→ {Esc(lbl)}</th>\n");
                sb.Append("              <th>Support</th>\n            </tr>\n          </thead>\n          <tbody>\n");

                for (int row = 0; row < n; row++)
                {
                    int rowSupport = cm.PerClass?[row]?.Support ?? 0;
                    sb.Append($"            <tr>\n              <td class=\"cm-row-label\">{Esc(cm.Labels[row])} ({rowSupport})</td>\n");
                    for (int col = 0; col < n; col++)
                    {
                        int val = cm.Matrix[row, col];
                        bool isDiag = row == col;
                        string tdCls = isDiag ? "cm-hit" : (val > 0 ? "cm-miss" : "");
                        string suffix = isDiag ? " ✓" : "";
                        sb.Append($"              <td class=\"{tdCls}\">{val}{suffix}</td>\n");
                    }
                    sb.Append($"              <td style=\"font-family:var(--mono);font-size:12px;color:var(--text2)\">{rowSupport}</td>\n            </tr>\n");
                }
                sb.Append("          </tbody>\n        </table>\n      </div>\n");

                // Per-class cards
                sb.Append("      <div class=\"per-class\">\n");
                string[] pcBadge = { "PASS CLASS", "DEFECT CLASS", "CLASS", "CLASS", "CLASS" };
                string[] pcFill = { "fill-ok", "fill-ng", "fill-cls", "fill-cls", "fill-cls" };
                string[] pcBadgeCls = { "badge-ok", "badge-ng", "badge-cls", "badge-cls", "badge-cls" };
                string[] pcValColor = { "color:var(--greenlt)", "color:var(--redlt)", "color:var(--accentlt)", "color:var(--accentlt)", "color:var(--accentlt)" };

                for (int c = 0; c < n; c++)
                {
                    var m = cm.PerClass[c];
                    string fill = pcFill[Math.Min(c, pcFill.Length - 1)];
                    string badge = pcBadge[Math.Min(c, pcBadge.Length - 1)];
                    string badgeCls = pcBadgeCls[Math.Min(c, pcBadgeCls.Length - 1)];
                    string valColor = pcValColor[Math.Min(c, pcValColor.Length - 1)];

                    sb.Append($@"        <div class=""pc-card"">
          <div class=""pc-label-row"">
            <div class=""pc-label-name"">{Esc(m.Label)}</div>
            <div class=""pc-badge {badgeCls}"">{badge}</div>
          </div>
          <div class=""pc-metrics"">
            <div>
              <div class=""pc-metric-label"">F1 Score</div>
              <div class=""pc-metric-val"" style=""{valColor}"">{m.F1:P1}</div>
              <div class=""pc-bar""><div class=""pc-fill {fill}"" style=""width:{m.F1 * 100:F1}%""></div></div>
            </div>
            <div>
              <div class=""pc-metric-label"">Precision</div>
              <div class=""pc-metric-val"" style=""{valColor}"">{m.Precision:P1}</div>
              <div class=""pc-bar""><div class=""pc-fill {fill}"" style=""width:{m.Precision * 100:F1}%""></div></div>
            </div>
            <div>
              <div class=""pc-metric-label"">Recall</div>
              <div class=""pc-metric-val"" style=""{valColor}"">{m.Recall:P1}</div>
              <div class=""pc-bar""><div class=""pc-fill {fill}"" style=""width:{m.Recall * 100:F1}%""></div></div>
            </div>
          </div>
        </div>
");
                }
                sb.Append("      </div>\n    </div>\n  </section>\n");
            }

            // ── 07 OUTPUT FILES ───────────────────────────────────────────────
            var artifacts = r.ArtifactPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            if (artifacts.Any())
            {
                sb.Append(@"
  <section class=""section"">
    <div class=""section-head""><span class=""section-num"">07</span><span class=""section-title"">Saved Artifacts</span></div>
    <div class=""output-files"">
");
                var iconMap = new Dictionary<string, (string icon, string tagCls, string tagLabel)>
                {
                    { ".zip",  ("📦", "ftag-zip",  "MODEL")      },
                    { ".json", ("⚙️",  "ftag-json", "ROI CONFIG") },
                    { ".html", ("📊", "ftag-html", "REPORT")     },
                    { ".csv",  ("📝", "ftag-csv",  "TUNING LOG") },
                };

                foreach (var path in artifacts)
                {
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    var (icon, tagCls, tagLabel) = iconMap.TryGetValue(ext, out var v) ? v : ("📄", "ftag-csv", "FILE");
                    var dir = Path.GetDirectoryName(path) ?? "";
                    var fname = Path.GetFileName(path);

                    sb.Append($@"      <div class=""file-row"">
        <div class=""file-left"">
          <div class=""file-icon"">{icon}</div>
          <div>
            <div class=""file-name"">{Esc(fname)}</div>
            <div class=""file-path"">{Esc(dir)}</div>
          </div>
        </div>
        <div class=""file-right""><span class=""ftag {tagCls}"">{tagLabel}</span></div>
      </div>
");
                }
                sb.Append("    </div>\n  </section>\n");
            }

            // ── FOOTER ─────────────────────────────────────────────────────────
            sb.Append($@"
  <footer class=""footer"">
    <div class=""footer-text"">ML.NET · Image_Checker · {r.Dataset?.ClassCounts?.Count ?? 0} Classes · {r.SessionEnd:yyyy-MM-dd}</div>
    <div class=""footer-text"">Generated by ReportGenerator.Write()</div>
  </footer>

</div>
</body>
</html>
");
            return sb.ToString();
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}