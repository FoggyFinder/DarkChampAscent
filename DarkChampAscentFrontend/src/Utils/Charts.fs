module Charts

open Feliz
open Fable.Core.JsInterop

let private toJsArray (xs: string list) =
    "[" + (xs |> List.map (fun s -> "\"" + s + "\"") |> String.concat ",") + "]"

let private toJsNumArray (xs: float list) =
    "[" + (xs |> List.map string |> String.concat ",") + "]"

let private buildInitScript (canvasId: string) (labels: string list) (data: float list) (colors: string list) =
    let labelsJs = toJsArray labels
    let dataJs   = toJsNumArray data
    let colorsJs = toJsArray colors
    "(function() {" +
    "if (typeof ChartDataLabels === 'undefined') { console.error('ChartDataLabels not loaded'); return; }" +
    "Chart.register(ChartDataLabels);" +
    "var canvas = document.getElementById('" + canvasId + "');" +
    "if (canvas) {" +
    "if (window['_chart_" + canvasId + "']) { try { window['_chart_" + canvasId + "'].destroy(); } catch(e) {} }" +
    "window['_chart_" + canvasId + "'] = new Chart(canvas.getContext('2d'), {" +
    "type: 'pie'," +
    "data: { labels: " + labelsJs + ", datasets: [{ data: " + dataJs + ", backgroundColor: " + colorsJs + ", borderColor: '#222', borderWidth: 2 }] }," +
    "options: { responsive: false, plugins: {" +
    "legend: { position: 'bottom', labels: { color: '#fff', usePointStyle: true, boxWidth: 12, padding: 12 } }," +
    "tooltip: { callbacks: { label: function(ctx) { var sum = ctx.chart.data.datasets[0].data.reduce(function(a,b){return a+b;},0); return ctx.label+': '+ctx.raw+' ('+Math.round(ctx.raw/sum*100)+'%)'; } } }," +
    "datalabels: { color:'#fff', formatter: function(v,ctx){ var s=ctx.chart.data.datasets[0].data.reduce(function(a,b){return a+b;},0); return Math.round(v/s*100)+'%'; }, anchor:'end', align:'start', offset:10, font:{weight:'700'}, clamp:true }" +
    "}}}); }" +   // closes: plugins{}, options{}, Chart({})  then if(canvas){}
    "})();"

[<ReactComponent>]
let PieChart (canvasId: string) (widthPx: int) (heightPx: int) (labels: string list) (data: float list) (colors: string list) =
    React.useEffect((fun () ->
        let initScript = buildInitScript canvasId labels data colors
        emitJsStatement (initScript) "eval($0);"
        ()
    ), [||])

    Html.canvas [
        prop.id canvasId
        prop.width widthPx
        prop.height heightPx
        prop.className "chart-canvas"
    ]