module RewardsChartHelpers

open Falco.Markup
open System.Globalization

let private escapeJs (s: string) =
  s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")

let private toJsArrayOfStrings (xs: string list) =
  "[" + (xs |> List.map (fun s -> sprintf "\"%s\"" (escapeJs s)) |> String.concat ",") + "]"

let private toJsArrayOfNumbers (xs: float list) =
  "[" + (xs |> List.map (fun n -> n.ToString(CultureInfo.InvariantCulture)) |> String.concat ",") + "]"

let chartJsLibScripts =
  [
    Elem.script [ Attr.src "https://cdn.jsdelivr.net/npm/chart.js@4.3.0/dist/chart.umd.min.js" ] []
    Elem.script [ Attr.src "https://cdn.jsdelivr.net/npm/chartjs-plugin-datalabels@2.2.0/dist/chartjs-plugin-datalabels.min.js" ] []
  ]

/// Renders a canvas wrapped in a sized container. Use `fixedSize = Some(widthPx,heightPx)` for fixed-mode,
/// or `fixedSize = None` and `wrapperHeightPx` to use responsive wrapper mode (Chart fills wrapper).
let pieCanvasWithWrapper (canvasId: string) (wrapperWidthCss: string) (wrapperHeightPx: int) (fixedSize: (int*int) option) =
  // wrapper div ensures Chart.js measures a stable parent
  let wrapperStyle = sprintf "max-width:%s; height:%ipx; margin:0 auto; position:relative;" wrapperWidthCss wrapperHeightPx
  let canvasAttrs =
    match fixedSize with
    | Some (w, h) ->
        // set width/height attributes (pixel) and allow Chart to run in non-responsive fixed mode
        [ Attr.id canvasId; Attr.create "width" (string w); Attr.create "height" (string h); Attr.class' "chart-canvas" ]
    | None ->
        // responsive canvas that will be sized by the wrapper
        [ Attr.id canvasId; Attr.style "width:100%; height:100%; display:block;"; Attr.class' "chart-canvas" ]
  Elem.div [ Attr.style wrapperStyle; Attr.class' "chart-wrapper" ] [
    Elem.canvas canvasAttrs []
  ]

let pieInitScript (canvasId: string) (labels: string list) (data: float list) (colors: string list) (includeDataLabels: bool) (fixedMode: bool) =
  let labelsJs = toJsArrayOfStrings labels
  let dataJs = toJsArrayOfNumbers data
  let colorsJs = toJsArrayOfStrings colors

  let datalabelsConfig =
    if includeDataLabels then
      """
        datalabels: {
          color: '#ffffff',
          formatter: (value, ctx) => {
            const sum = ctx.chart.data.datasets[0].data.reduce((a,b)=>a+b,0);
            return Math.round(value / sum * 100) + ' %';
          },
          anchor: 'end',
          align: 'start',
          offset: 10,
          font: { weight: '700' },
          clamp: true
        },
      """
    else ""

  // If fixedMode = true we set responsive:false, otherwise responsive:true + maintainAspectRatio:false
  let responsivePart =
    if fixedMode then "responsive: false," else "responsive: true, maintainAspectRatio: false,"

  let script =
    sprintf """
(function(){
  const labels = %s;
  const data = %s;
  const colors = %s;
  const canvas = document.getElementById('%s');
  if (!canvas) return;
  const ctx = canvas.getContext('2d');

  const config = {
    type: 'pie',
    data: {
      labels: labels,
      datasets: [{
        data: data,
        backgroundColor: colors,
        borderColor: '#222',
        borderWidth: 2
      }]
    },
    options: {
      %s
      plugins: {
        legend: {
          position: 'bottom',
          labels: {
            color: '#fff',
            usePointStyle: true,
            boxWidth: 12,
            padding: 12
          }
        },
        tooltip: {
          callbacks: {
            label: function(context) {
              const value = context.raw;
              const sum = context.chart.data.datasets[0].data.reduce((a,b)=>a+b,0);
              const pct = (value / sum * 100).toFixed(1) + '%%';
              return context.label + ': ' + value + ' (' + pct + ')';
            }
          }
        },
        %s
      }
    }
  };

  if (window['_chart_%s']) {
    try { window['_chart_%s'].destroy(); } catch(e) {}
  }

  window['_chart_%s'] = new Chart(ctx, config);
})();
""" 
  let sc = script labelsJs dataJs colorsJs canvasId responsivePart datalabelsConfig canvasId canvasId canvasId

  Elem.script [] [ Text.raw sc ]

/// High-level helper: wrapperWidthCss like "700px" or "100%". If you want fixed pixel canvas specify fixedCanvas = Some(widthPx,heightPx).
let renderPieChart
    (canvasId: string)
    (labels: string list)
    (data: float list)
    (colors: string list)
    (wrapperWidthCss: string)
    (wrapperHeightPx: int)
    (includeDataLabels: bool)
    (includeLibs: bool)
    (fixedCanvas: (int*int) option) =
    [
        if includeLibs then yield! chartJsLibScripts
        pieCanvasWithWrapper canvasId wrapperWidthCss wrapperHeightPx fixedCanvas
        pieInitScript canvasId labels data colors includeDataLabels fixedCanvas.IsSome
    ]