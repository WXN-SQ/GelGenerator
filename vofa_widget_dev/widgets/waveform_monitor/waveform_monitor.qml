import QtQuick 2.12
import QtQuick.Controls 2.12
import MyModules 1.0

ResizableRectangle {
    id: root

    property string path: "waveform_monitor"
    property string titleText: qsTr("波形监视器")
    property bool titleEditing: false
    property bool lightTheme: false
    property bool paused: false
    property bool following: true
    property bool autoRange: true
    property bool showGrid: true
    property bool showPoints: false
    property int cacheLimit: 1000
    property real lineWidth: 1.5
    property real manualYMin: -10
    property real manualYMax: 10

    property string curve1Name: qsTr("曲线 1")
    property string curve2Name: qsTr("曲线 2")
    property string curve3Name: qsTr("曲线 3")
    property string curve4Name: qsTr("曲线 4")
    property color curve1Color: "#3fd3f2"
    property color curve2Color: "#68dfb2"
    property color curve3Color: "#f2ba55"
    property color curve4Color: "#b58cff"
    property bool curve1Visible: true
    property bool curve2Visible: true
    property bool curve3Visible: true
    property bool curve4Visible: true

    property var records: []
    property var lastValues: [undefined, undefined, undefined, undefined]
    property double localTimeOrigin: 0
    property real xViewMin: 0
    property real xViewMax: 10
    property real yViewMin: -1
    property real yViewMax: 1
    property real dragStartX: 0
    property real dragStartY: 0
    property real dragXMin: 0
    property real dragXMax: 10
    property real dragYMin: -1
    property real dragYMax: 1
    property int hoverIndex: -1

    readonly property color panelBase: lightTheme ? "#eef4f7" : "#0e1620"
    readonly property color panelBorder: lightTheme ? "#9cb8c5" : "#345267"
    readonly property color gradientTop: lightTheme ? "#ffffff" : "#1a2937"
    readonly property color gradientBottom: lightTheme ? "#dfe9ee" : "#0a1119"
    readonly property color headerColor: lightTheme ? "#e4eff4" : "#142532"
    readonly property color primaryText: lightTheme ? "#17313e" : "#e2f2f8"
    readonly property color secondaryText: lightTheme ? "#537687" : "#7197a8"
    readonly property color dividerColor: lightTheme ? "#a7c3cf" : "#315064"
    readonly property color plotColor: lightTheme ? "#fbfdfe" : "#091119"
    readonly property color gridColor: lightTheme ? "#dbe7ec" : "#172b37"
    readonly property color gridMajorColor: lightTheme ? "#b8cdd5" : "#294757"
    readonly property color buttonColor: lightTheme ? "#dceaf0" : "#192f3d"
    readonly property color buttonHoverColor: lightTheme ? "#c5e1ea" : "#205268"

    property Item ref: Loader {
        active: false
        sourceComponent: Component {
            Item {
                property var ref_x_menu: x_menu
                property var ref_ch1_menu: ch1_menu
                property var ref_ch2_menu: ch2_menu
                property var ref_ch3_menu: ch3_menu
                property var ref_ch4_menu: ch4_menu
            }
        }
    }

    width: g_settings.applyHScale(720)
    height: g_settings.applyVScale(460)
    minimumWidth: g_settings.applyHScale(480)
    minimumHeight: g_settings.applyVScale(340)
    radius: g_settings.applyHScale(9)
    color: panelBase
    border.color: panelBorder
    border.width: g_settings.applyHScale(1)

    Rectangle {
        anchors.fill: parent
        anchors.margins: root.border.width
        radius: root.radius
        z: -1
        gradient: Gradient {
            GradientStop { position: 0.0; color: root.gradientTop }
            GradientStop { position: 1.0; color: root.gradientBottom }
        }
    }

    Item {
        id: header
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        height: g_settings.applyVScale(46)
        z: 4

        Rectangle { anchors.fill: parent; radius: root.radius; color: root.headerColor }
        Rectangle {
            anchors.left: parent.left; anchors.right: parent.right; anchors.bottom: parent.bottom
            height: g_settings.applyVScale(1); color: root.dividerColor
        }
        Rectangle {
            anchors.left: parent.left; anchors.leftMargin: g_settings.applyHScale(12)
            anchors.verticalCenter: parent.verticalCenter
            width: g_settings.applyHScale(5); height: g_settings.applyVScale(23)
            radius: width / 2; color: "#34c9ed"
        }

        Item {
            anchors.left: parent.left; anchors.leftMargin: g_settings.applyHScale(28)
            anchors.right: toolbar.left; anchors.rightMargin: g_settings.applyHScale(12)
            anchors.verticalCenter: parent.verticalCenter; height: g_settings.applyVScale(32)

            MyText {
                anchors.fill: parent; verticalAlignment: Text.AlignVCenter
                text: root.titleText; color: root.primaryText
                font.pixelSize: g_settings.fontPixelSizeBig
                elide: Text.ElideRight; visible: !root.titleEditing
            }
            MyMouseArea {
                anchors.fill: parent; enabled: !root.titleEditing; cursorShape: Qt.IBeamCursor
                onDoubleClicked: {
                    root.titleEditing = true
                    titleInput.text = root.titleText
                    titleInput.forceActiveFocus(); titleInput.selectAll()
                }
            }
            TextField {
                id: titleInput
                anchors.fill: parent; visible: root.titleEditing
                color: root.primaryText; selectionColor: "#239aba"; selectedTextColor: "white"
                font.pixelSize: g_settings.fontPixelSizeBig; selectByMouse: true
                background: Rectangle { color: "transparent"; border.color: "#31b9d9"; radius: 3 }
                onAccepted: root.finishTitleEditing(text)
                onActiveFocusChanged: if (!activeFocus && root.titleEditing) root.finishTitleEditing(text)
            }
        }

        Row {
            id: toolbar
            anchors.right: parent.right; anchors.rightMargin: g_settings.applyHScale(8)
            anchors.verticalCenter: parent.verticalCenter
            spacing: g_settings.applyHScale(5)

            Repeater {
                model: ["pause", "clear", "follow", "auto", "theme"]
                Rectangle {
                    width: modelData === "theme" ? g_settings.applyHScale(58) : g_settings.applyHScale(52)
                    height: g_settings.applyVScale(27); radius: height / 2
                    color: toolMouse.containsMouse ? root.buttonHoverColor : root.buttonColor
                    border.color: root.panelBorder
                    MyText {
                        anchors.centerIn: parent; text: root.toolText(modelData)
                        color: root.toolActive(modelData) ? "#13a7c9" : root.primaryText
                        font.pixelSize: g_settings.fontPixelSizeSmall
                    }
                    MyMouseArea {
                        id: toolMouse; anchors.fill: parent; hoverEnabled: true
                        cursorShape: Qt.PointingHandCursor
                        onClicked: root.runTool(modelData)
                    }
                }
            }
        }
    }

    Row {
        id: legend
        anchors.left: parent.left; anchors.right: parent.right; anchors.top: header.bottom
        anchors.leftMargin: g_settings.applyHScale(54); anchors.rightMargin: g_settings.applyHScale(10)
        height: g_settings.applyVScale(30); spacing: g_settings.applyHScale(15)
        Repeater {
            model: 4
            Item {
                width: Math.max(g_settings.applyHScale(88), legend.width / 4 - legend.spacing)
                height: legend.height
                Rectangle {
                    anchors.left: parent.left; anchors.verticalCenter: parent.verticalCenter
                    width: g_settings.applyHScale(18); height: g_settings.applyVScale(3)
                    color: root.curveColor(index); opacity: root.curveVisible(index) ? 1 : 0.25
                }
                MyText {
                    anchors.left: parent.left; anchors.leftMargin: g_settings.applyHScale(24)
                    anchors.right: parent.right; anchors.verticalCenter: parent.verticalCenter
                    text: root.curveName(index); color: root.curveVisible(index) ? root.primaryText : root.secondaryText
                    font.pixelSize: g_settings.fontPixelSizeSmall; elide: Text.ElideRight
                }
            }
        }
    }

    Canvas {
        id: chart
        anchors.left: parent.left; anchors.right: parent.right
        anchors.top: legend.bottom; anchors.bottom: footer.top
        anchors.margins: g_settings.applyHScale(5)
        onWidthChanged: requestPaint()
        onHeightChanged: requestPaint()
        onPaint: root.paintChart(getContext("2d"), width, height)

        MyMouseArea {
            id: chartMouse
            anchors.fill: parent; hoverEnabled: true; acceptedButtons: Qt.LeftButton
            cursorShape: pressed ? Qt.ClosedHandCursor : Qt.CrossCursor
            onPressed: {
                root.dragStartX = mouseX; root.dragStartY = mouseY
                root.dragXMin = root.xViewMin; root.dragXMax = root.xViewMax
                root.dragYMin = root.yViewMin; root.dragYMax = root.yViewMax
            }
            onPositionChanged: {
                root.updateHover(mouseX, mouseY, width, height)
                if (pressed) root.panView(mouseX, mouseY, width, height)
            }
            onExited: { root.hoverIndex = -1; chart.requestPaint() }
            onDoubleClicked: if (mouse.button === Qt.LeftButton) root.resetView()
            onWheel: root.zoomView(mouseX, mouseY, wheel.angleDelta.y, width, height)
        }
    }

    Rectangle {
        id: footer
        anchors.left: parent.left; anchors.right: parent.right; anchors.bottom: parent.bottom
        anchors.margins: g_settings.applyHScale(7); height: g_settings.applyVScale(30)
        radius: g_settings.applyHScale(5); color: root.lightTheme ? "#e5eff3" : "#122531"
        border.color: root.panelBorder
        MyText {
            anchors.left: parent.left; anchors.leftMargin: g_settings.applyHScale(9)
            anchors.verticalCenter: parent.verticalCenter
            text: (x_menu.bind_obj ? qsTr("横轴：序号") : qsTr("横轴：时间 (s)")) +
                  "    " + qsTr("点数") + ": " + root.records.length + " / " + root.cacheLimit
            color: root.secondaryText; font.pixelSize: g_settings.fontPixelSizeSmall
        }
        MyText {
            anchors.right: parent.right; anchors.rightMargin: g_settings.applyHScale(9)
            anchors.verticalCenter: parent.verticalCenter
            text: root.hoverText(); color: root.primaryText
            font.pixelSize: g_settings.fontPixelSizeSmall
        }
    }

    MyIconMenu {
        id: menu
        DeleteMenuItem { target: root }
        FillParentMenu { target: root }
        MyMenuItem {
            text_center: true; text: root.lightTheme ? qsTr("切换为深色") : qsTr("切换为浅色")
            checked: root.lightTheme; onTriggered: root.lightTheme = !root.lightTheme
        }
        SettingsMenu {
            title: qsTr("波形设置")
            MyMenuItem {
                text: qsTr("缓存点数") + ":"; plus_minus_on: true; value_editable: true
                value_text: "" + root.cacheLimit
                onPlus_triggered: root.setCacheLimit(root.cacheLimit + 100)
                onMinus_triggered: root.setCacheLimit(root.cacheLimit - 100)
                onValue_inputed: root.setCacheLimit(parseInt(text))
            }
            MyMenuItem {
                text: qsTr("曲线线宽") + ":"; plus_minus_on: true; value_editable: true
                value_text: "" + root.lineWidth
                onPlus_triggered: { root.lineWidth = Math.min(8, root.lineWidth + 0.5); chart.requestPaint() }
                onMinus_triggered: { root.lineWidth = Math.max(0.5, root.lineWidth - 0.5); chart.requestPaint() }
                onValue_inputed: { root.lineWidth = Math.max(0.5, Math.min(8, parseFloat(text))); chart.requestPaint() }
            }
            MyMenuItem {
                text: qsTr("显示网格"); checked: root.showGrid
                onTriggered: { root.showGrid = !root.showGrid; chart.requestPaint() }
            }
            MyMenuItem {
                text: qsTr("显示数据点"); checked: root.showPoints
                onTriggered: { root.showPoints = !root.showPoints; chart.requestPaint() }
            }
            MyMenuItem {
                text: qsTr("自动量程"); checked: root.autoRange
                onTriggered: { root.autoRange = !root.autoRange; root.updateYRange(); chart.requestPaint() }
            }
            MyMenuItem {
                text: qsTr("Y 最小值") + ":"; value_editable: true
                value_text: "" + root.manualYMin
                onValue_inputed: root.setManualY(parseFloat(text), root.manualYMax)
            }
            MyMenuItem {
                text: qsTr("Y 最大值") + ":"; value_editable: true
                value_text: "" + root.manualYMax
                onValue_inputed: root.setManualY(root.manualYMin, parseFloat(text))
            }
        }
        MyMenuSeparator { }
        ChMenu {
            id: x_menu; checked: bind_obj; color: "#78909c"
            title: qsTr("横轴通道（序号）") + (bind_obj ? (" → " + bind_obj.name) : "")
            onBind_objChanged: root.clearData()
        }
        MyMenu {
            title: root.curve1Name
            ChMenu { id: ch1_menu; checked: bind_obj; color: root.curve1Color; title: qsTr("绑定通道") + (bind_obj ? (" → " + bind_obj.name) : ""); onBind_objChanged: root.handleCurveBinding(0) }
            MyMenuItem { text: qsTr("显示曲线"); checked: root.curve1Visible; onTriggered: root.toggleCurve(0) }
            MyMenuItem { text: qsTr("名称") + ":"; value_editable: true; value_text: root.curve1Name; onValue_inputed: root.setCurveName(0, text) }
            MyColorMenuItem { text: qsTr("曲线颜色..."); target_obj: root; target_name: "curve1Color"; onTriggered: chart.requestPaint() }
        }
        MyMenu {
            title: root.curve2Name
            ChMenu { id: ch2_menu; checked: bind_obj; color: root.curve2Color; title: qsTr("绑定通道") + (bind_obj ? (" → " + bind_obj.name) : ""); onBind_objChanged: root.handleCurveBinding(1) }
            MyMenuItem { text: qsTr("显示曲线"); checked: root.curve2Visible; onTriggered: root.toggleCurve(1) }
            MyMenuItem { text: qsTr("名称") + ":"; value_editable: true; value_text: root.curve2Name; onValue_inputed: root.setCurveName(1, text) }
            MyColorMenuItem { text: qsTr("曲线颜色..."); target_obj: root; target_name: "curve2Color"; onTriggered: chart.requestPaint() }
        }
        MyMenu {
            title: root.curve3Name
            ChMenu { id: ch3_menu; checked: bind_obj; color: root.curve3Color; title: qsTr("绑定通道") + (bind_obj ? (" → " + bind_obj.name) : ""); onBind_objChanged: root.handleCurveBinding(2) }
            MyMenuItem { text: qsTr("显示曲线"); checked: root.curve3Visible; onTriggered: root.toggleCurve(2) }
            MyMenuItem { text: qsTr("名称") + ":"; value_editable: true; value_text: root.curve3Name; onValue_inputed: root.setCurveName(2, text) }
            MyColorMenuItem { text: qsTr("曲线颜色..."); target_obj: root; target_name: "curve3Color"; onTriggered: chart.requestPaint() }
        }
        MyMenu {
            title: root.curve4Name
            ChMenu { id: ch4_menu; checked: bind_obj; color: root.curve4Color; title: qsTr("绑定通道") + (bind_obj ? (" → " + bind_obj.name) : ""); onBind_objChanged: root.handleCurveBinding(3) }
            MyMenuItem { text: qsTr("显示曲线"); checked: root.curve4Visible; onTriggered: root.toggleCurve(3) }
            MyMenuItem { text: qsTr("名称") + ":"; value_editable: true; value_text: root.curve4Name; onValue_inputed: root.setCurveName(3, text) }
            MyColorMenuItem { text: qsTr("曲线颜色..."); target_obj: root; target_name: "curve4Color"; onTriggered: chart.requestPaint() }
        }
    }

    Connections {
        target: root.mouse
        onClicked: if (mouse.button === Qt.RightButton) menu.popup()
    }
    Connections { target: sys_manager; onNeed_update: root.captureSample() }
    onLightThemeChanged: chart.requestPaint()
    onCurve1ColorChanged: chart.requestPaint()
    onCurve2ColorChanged: chart.requestPaint()
    onCurve3ColorChanged: chart.requestPaint()
    onCurve4ColorChanged: chart.requestPaint()

    function curveMenu(index) { return index === 0 ? ch1_menu : (index === 1 ? ch2_menu : (index === 2 ? ch3_menu : ch4_menu)) }
    function curveName(index) { return index === 0 ? curve1Name : (index === 1 ? curve2Name : (index === 2 ? curve3Name : curve4Name)) }
    function curveColor(index) { return index === 0 ? curve1Color : (index === 1 ? curve2Color : (index === 2 ? curve3Color : curve4Color)) }
    function curveVisible(index) { return index === 0 ? curve1Visible : (index === 1 ? curve2Visible : (index === 2 ? curve3Visible : curve4Visible)) }
    function curveColorProperty(index) { return index === 0 ? "curve1Color" : (index === 1 ? "curve2Color" : (index === 2 ? "curve3Color" : "curve4Color")) }
    function setCurveName(index, value) {
        var name = ("" + value).trim(); if (!name.length) name = qsTr("曲线 ") + (index + 1)
        if (index === 0) curve1Name = name; else if (index === 1) curve2Name = name
        else if (index === 2) curve3Name = name; else curve4Name = name
    }
    function handleCurveBinding(index) {
        resetLastValues()
        var channel = curveMenu(index)
        var defaultName = qsTr("曲线 ") + (index + 1)
        if (channel.bind_obj && curveName(index) === defaultName)
            setCurveName(index, channel.bind_obj.name)
    }
    function toggleCurve(index) {
        if (index === 0) curve1Visible = !curve1Visible; else if (index === 1) curve2Visible = !curve2Visible
        else if (index === 2) curve3Visible = !curve3Visible; else curve4Visible = !curve4Visible
        updateYRange(); chart.requestPaint()
    }
    function finishTitleEditing(value) {
        var valueText = ("" + value).trim(); titleText = valueText.length ? valueText : qsTr("波形监视器")
        titleEditing = false
    }
    function toolText(tool) {
        if (tool === "pause") return paused ? qsTr("继续") : qsTr("暂停")
        if (tool === "clear") return qsTr("清空")
        if (tool === "follow") return qsTr("跟随")
        if (tool === "auto") return qsTr("自动")
        return lightTheme ? qsTr("浅色") : qsTr("深色")
    }
    function toolActive(tool) { return (tool === "pause" && paused) || (tool === "follow" && following) || (tool === "auto" && autoRange) }
    function runTool(tool) {
        if (tool === "pause") paused = !paused
        else if (tool === "clear") clearData()
        else if (tool === "follow") { following = true; fitXToData(); updateYRange(); chart.requestPaint() }
        else if (tool === "auto") { autoRange = !autoRange; updateYRange(); chart.requestPaint() }
        else lightTheme = !lightTheme
    }
    function setCacheLimit(value) {
        if (!isFinite(value)) return
        cacheLimit = Math.max(100, Math.min(10000, Math.round(value)))
        if (records.length > cacheLimit) records = records.slice(records.length - cacheLimit)
        fitXToData(); updateYRange(); chart.requestPaint()
    }
    function setManualY(minimum, maximum) {
        if (!isFinite(minimum) || !isFinite(maximum) || minimum >= maximum) return
        manualYMin = minimum; manualYMax = maximum; autoRange = false
        yViewMin = minimum; yViewMax = maximum; chart.requestPaint()
    }
    function resetLastValues() { lastValues = [undefined, undefined, undefined, undefined] }
    function clearData() {
        records = []; resetLastValues(); localTimeOrigin = 0; hoverIndex = -1
        xViewMin = 0; xViewMax = 10; yViewMin = autoRange ? -1 : manualYMin; yViewMax = autoRange ? 1 : manualYMax
        following = true; chart.requestPaint()
    }
    function captureSample() {
        if (paused) return
        var values = []; var changed = false; var boundCount = 0
        for (var i = 0; i < 4; ++i) {
            var channel = curveMenu(i)
            var value = channel && channel.bind_obj ? Number(channel.bind_obj.value) : NaN
            values.push(value)
            if (isFinite(value)) {
                ++boundCount
                if (lastValues[i] === undefined || value !== lastValues[i]) changed = true
            }
        }
        if (!boundCount || !changed) return
        var xValue
        if (x_menu.bind_obj) xValue = Number(x_menu.bind_obj.value)
        else {
            var now = Date.now()
            if (!localTimeOrigin) localTimeOrigin = now
            xValue = (now - localTimeOrigin) / 1000.0
        }
        if (!isFinite(xValue)) return
        lastValues = values.slice(0)
        var next = records.slice(0); next.push({ "x": xValue, "v": values })
        if (next.length > cacheLimit) next = next.slice(next.length - cacheLimit)
        records = next
        if (following) fitXToData()
        updateYRange(); chart.requestPaint()
    }
    function fitXToData() {
        if (!records.length) { xViewMin = 0; xViewMax = 10; return }
        xViewMin = records[0].x; xViewMax = records[records.length - 1].x
        if (xViewMax <= xViewMin) { xViewMin -= 0.5; xViewMax += 0.5 }
    }
    function updateYRange() {
        if (!autoRange) { yViewMin = manualYMin; yViewMax = manualYMax; return }
        var minValue = Infinity, maxValue = -Infinity
        for (var r = 0; r < records.length; ++r) {
            if (records[r].x < xViewMin || records[r].x > xViewMax) continue
            for (var c = 0; c < 4; ++c) {
                if (!curveVisible(c) || !isFinite(records[r].v[c])) continue
                minValue = Math.min(minValue, records[r].v[c]); maxValue = Math.max(maxValue, records[r].v[c])
            }
        }
        if (!isFinite(minValue)) { yViewMin = -1; yViewMax = 1; return }
        var span = maxValue - minValue
        if (span <= 0) span = Math.max(1, Math.abs(maxValue) * 0.1)
        yViewMin = minValue - span * 0.08; yViewMax = maxValue + span * 0.08
    }
    function plotRect(width, height) { return { "l": 52, "r": width - 10, "t": 8, "b": height - 30 } }
    function paintChart(ctx, width, height) {
        ctx.clearRect(0, 0, width, height)
        var p = plotRect(width, height); var pw = Math.max(1, p.r - p.l), ph = Math.max(1, p.b - p.t)
        ctx.fillStyle = plotColor; ctx.fillRect(p.l, p.t, pw, ph)
        ctx.strokeStyle = panelBorder; ctx.lineWidth = 1; ctx.strokeRect(p.l, p.t, pw, ph)
        ctx.font = Math.max(9, g_settings.applyFontScale(10)) + "px " + g_settings.fontFamilyNumber
        for (var i = 0; i <= 10; ++i) {
            var px = p.l + pw * i / 10, py = p.t + ph * i / 10
            if (showGrid) {
                ctx.strokeStyle = i === 5 ? gridMajorColor : gridColor; ctx.beginPath()
                ctx.moveTo(px, p.t); ctx.lineTo(px, p.b); ctx.moveTo(p.l, py); ctx.lineTo(p.r, py); ctx.stroke()
            }
            ctx.fillStyle = secondaryText; ctx.textAlign = "center"; ctx.textBaseline = "top"
            ctx.fillText(formatNumber(xViewMin + (xViewMax - xViewMin) * i / 10), px, p.b + 5)
            ctx.textAlign = "right"; ctx.textBaseline = "middle"
            ctx.fillText(formatNumber(yViewMax - (yViewMax - yViewMin) * i / 10), p.l - 6, py)
        }
        ctx.save(); ctx.beginPath(); ctx.rect(p.l, p.t, pw, ph); ctx.clip()
        for (var c = 0; c < 4; ++c) {
            if (!curveVisible(c)) continue
            ctx.strokeStyle = curveColor(c); ctx.fillStyle = curveColor(c); ctx.lineWidth = lineWidth
            var started = false; ctx.beginPath()
            for (var r = 0; r < records.length; ++r) {
                var rec = records[r], value = rec.v[c]
                if (!isFinite(value) || rec.x < xViewMin || rec.x > xViewMax) continue
                var x = p.l + (rec.x - xViewMin) / (xViewMax - xViewMin) * pw
                var y = p.b - (value - yViewMin) / (yViewMax - yViewMin) * ph
                if (!started) { ctx.moveTo(x, y); started = true } else ctx.lineTo(x, y)
            }
            ctx.stroke()
            if (showPoints) {
                for (r = 0; r < records.length; ++r) {
                    rec = records[r]; value = rec.v[c]
                    if (!isFinite(value) || rec.x < xViewMin || rec.x > xViewMax) continue
                    x = p.l + (rec.x - xViewMin) / (xViewMax - xViewMin) * pw
                    y = p.b - (value - yViewMin) / (yViewMax - yViewMin) * ph
                    ctx.beginPath(); ctx.arc(x, y, 2.2, 0, Math.PI * 2, false); ctx.fill()
                }
            }
        }
        if (hoverIndex >= 0 && hoverIndex < records.length) {
            var hoverX = p.l + (records[hoverIndex].x - xViewMin) / (xViewMax - xViewMin) * pw
            ctx.strokeStyle = lightTheme ? "#687f89" : "#a6bdc7"; ctx.lineWidth = 1
            ctx.beginPath(); ctx.moveTo(hoverX, p.t); ctx.lineTo(hoverX, p.b); ctx.stroke()
        }
        ctx.restore()
    }
    function formatNumber(value) {
        if (!isFinite(value) || Math.abs(value) < 0.0000001) return "0"
        return "" + Number(value.toFixed(3))
    }
    function updateHover(mouseX, mouseY, width, height) {
        if (!records.length) return
        var p = plotRect(width, height); if (mouseX < p.l || mouseX > p.r || mouseY < p.t || mouseY > p.b) { hoverIndex = -1; chart.requestPaint(); return }
        var targetX = xViewMin + (mouseX - p.l) / (p.r - p.l) * (xViewMax - xViewMin)
        var best = -1, distance = Infinity
        for (var i = 0; i < records.length; ++i) { var d = Math.abs(records[i].x - targetX); if (d < distance) { distance = d; best = i } }
        hoverIndex = best; chart.requestPaint()
    }
    function hoverText() {
        if (hoverIndex < 0 || hoverIndex >= records.length) return ""
        var rec = records[hoverIndex], text = (x_menu.bind_obj ? "N=" : "t=") + formatNumber(rec.x)
        for (var i = 0; i < 4; ++i) if (curveVisible(i) && isFinite(rec.v[i])) text += "  " + curveName(i) + "=" + formatNumber(rec.v[i])
        return text
    }
    function zoomView(mouseX, mouseY, delta, width, height) {
        var p = plotRect(width, height); if (mouseX < p.l || mouseX > p.r || mouseY < p.t || mouseY > p.b) return
        var factor = delta > 0 ? 0.82 : 1.22
        var xAnchor = xViewMin + (mouseX - p.l) / (p.r - p.l) * (xViewMax - xViewMin)
        var yAnchor = yViewMax - (mouseY - p.t) / (p.b - p.t) * (yViewMax - yViewMin)
        xViewMin = xAnchor - (xAnchor - xViewMin) * factor; xViewMax = xAnchor + (xViewMax - xAnchor) * factor
        yViewMin = yAnchor - (yAnchor - yViewMin) * factor; yViewMax = yAnchor + (yViewMax - yAnchor) * factor
        following = false; autoRange = false; manualYMin = yViewMin; manualYMax = yViewMax; chart.requestPaint()
    }
    function panView(mouseX, mouseY, width, height) {
        var p = plotRect(width, height), dx = (mouseX - dragStartX) / (p.r - p.l), dy = (mouseY - dragStartY) / (p.b - p.t)
        var xSpan = dragXMax - dragXMin, ySpan = dragYMax - dragYMin
        xViewMin = dragXMin - dx * xSpan; xViewMax = dragXMax - dx * xSpan
        yViewMin = dragYMin + dy * ySpan; yViewMax = dragYMax + dy * ySpan
        manualYMin = yViewMin; manualYMax = yViewMax; following = false; autoRange = false; chart.requestPaint()
    }
    function resetView() { following = true; autoRange = true; fitXToData(); updateYRange(); chart.requestPaint() }

    function get_widget_ctx() {
        return { "path": path, "ctx": {
            ".": { "ctx": get_ctx(), "titleText": titleText, "lightTheme": lightTheme,
                "paused": paused, "following": following, "autoRange": autoRange,
                "showGrid": showGrid, "showPoints": showPoints, "cacheLimit": cacheLimit,
                "lineWidth": lineWidth, "manualYMin": manualYMin, "manualYMax": manualYMax,
                "curve1Name": curve1Name, "curve2Name": curve2Name, "curve3Name": curve3Name, "curve4Name": curve4Name,
                "curve1Color": curve1Color, "curve2Color": curve2Color, "curve3Color": curve3Color, "curve4Color": curve4Color,
                "curve1Visible": curve1Visible, "curve2Visible": curve2Visible,
                "curve3Visible": curve3Visible, "curve4Visible": curve4Visible },
            "x_menu": { "ctx": x_menu.get_ctx() },
            "ch1_menu": { "ctx": curveMenu(0).get_ctx() }, "ch2_menu": { "ctx": curveMenu(1).get_ctx() },
            "ch3_menu": { "ctx": curveMenu(2).get_ctx() }, "ch4_menu": { "ctx": curveMenu(3).get_ctx() }
        } }
    }
    function set_widget_ctx(ctx) {
        __set_ctx__(root, ctx.ctx, ref)
        var restoredFollowing = following, restoredAutoRange = autoRange
        clearData(); following = restoredFollowing; autoRange = restoredAutoRange
        setCacheLimit(cacheLimit)
        if (!autoRange) { yViewMin = manualYMin; yViewMax = manualYMax }
        chart.requestPaint()
    }
}
