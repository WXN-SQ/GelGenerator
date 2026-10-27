import QtQuick 2.12
import MyModules 1.0

ResizableRectangle {
    id: root

    property string path: "spot_deviation_target"
    property bool lightTheme: false
    readonly property color panelBase: lightTheme ? "#eef4f7" : "#101722"
    readonly property color panelBorder: lightTheme ? "#9cb8c5" : "#345267"
    readonly property color gradientTop: lightTheme ? "#ffffff" : "#182532"
    readonly property color gradientMiddle: lightTheme ? "#edf4f7" : "#101923"
    readonly property color gradientBottom: lightTheme ? "#dfe9ee" : "#0b1119"
    readonly property color primaryText: lightTheme ? "#17313e" : "#dcebf4"
    readonly property color secondaryText: lightTheme ? "#4f7181" : "#93b2c1"
    readonly property color dividerColor: lightTheme ? "#a7c3cf" : "#284357"
    readonly property color plotBackground: lightTheme ? "#f9fcfd" : "#091119"
    readonly property color plotBorder: lightTheme ? "#8caeba" : "#254151"
    readonly property color gridNormal: lightTheme ? "#d8e6eb" : "#172b37"
    readonly property color gridCenter: lightTheme ? "#a8c4ce" : "#31596c"
    readonly property color tickColor: lightTheme ? "#5e8797" : "#62889a"
    readonly property color targetColor: lightTheme ? "#0787a6" : "#37c8ed"
    readonly property color targetBright: lightTheme ? "#0b7892" : "#4dd9f5"
    readonly property color targetTickMajor: lightTheme ? "#126f85" : "#79e4f7"
    readonly property color targetTickMinor: lightTheme ? "#4d9aaa" : "#3b99b2"
    readonly property color statusPanelColor: lightTheme ? "#e6f0f4" : "#122531"
    readonly property color statusCardA: lightTheme ? "#f7fbfc" : "#172e3b"
    readonly property color statusCardB: lightTheme ? "#edf5f8" : "#142936"
    property real boundaryDiameter: 100
    property int historyLimit: 1000
    property var pointHistory: []
    property bool sampleInitialized: false
    property real lastTrackX: 0
    property real lastTrackY: 0
    property real lastSpotX: 0
    property real lastSpotY: 0
    property real latestDeltaX: 0
    property real latestDeltaY: 0
    property real latestRadius: 0
    property bool hasLatestSample: false

    property Item ref: Loader {
        active: false
        sourceComponent: Component {
            Item {
                property var ref_track_x_menu: track_x_menu
                property var ref_track_y_menu: track_y_menu
                property var ref_spot_x_menu: spot_x_menu
                property var ref_spot_y_menu: spot_y_menu
            }
        }
    }

    width: g_settings.applyHScale(420)
    height: g_settings.applyVScale(420)
    minimumWidth: g_settings.applyHScale(280)
    minimumHeight: g_settings.applyVScale(280)
    color: panelBase
    border.color: panelBorder
    border.width: g_settings.applyHScale(1)
    radius: g_settings.applyHScale(8)

    Rectangle {
        anchors.fill: parent
        anchors.margins: root.border.width
        radius: root.radius
        z: -1
        gradient: Gradient {
            GradientStop { position: 0.0; color: root.gradientTop }
            GradientStop { position: 0.55; color: root.gradientMiddle }
            GradientStop { position: 1.0; color: root.gradientBottom }
        }
    }

    Item {
        id: header
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        height: g_settings.applyVScale(34)
        z: 3

        Rectangle {
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            height: g_settings.applyVScale(1)
            color: root.dividerColor
        }

        MyText {
            anchors.left: parent.left
            anchors.leftMargin: g_settings.applyHScale(10)
            anchors.verticalCenter: parent.verticalCenter
            text: qsTr("光斑脱靶量")
            color: root.primaryText
            font.pixelSize: g_settings.fontPixelSizeNormal
        }

        Rectangle {
            id: clearButton
            anchors.right: themeButton.left
            anchors.rightMargin: g_settings.applyHScale(6)
            anchors.verticalCenter: parent.verticalCenter
            width: g_settings.applyHScale(54)
            height: g_settings.applyVScale(24)
            radius: g_settings.applyHScale(3)
            color: clearMouse.containsMouse ?
                       (root.lightTheme ? "#b8dfe9" : "#167d9b") :
                       (root.lightTheme ? "#d8e9ef" : "#1b3443")
            border.color: clearMouse.containsMouse ?
                              (root.lightTheme ? "#5a9fb2" : "#45d7ff") :
                              (root.lightTheme ? "#8eb4c2" : "#396277")
            border.width: g_settings.applyHScale(1)

            MyText {
                anchors.centerIn: parent
                text: qsTr("清空")
                color: root.lightTheme ? "#17495a" :
                                         (clearMouse.containsMouse ? "white" : "#c4dce8")
                font.pixelSize: g_settings.fontPixelSizeSmall
            }

            MyMouseArea {
                id: clearMouse
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: root.clearHistory()
            }
        }

        Rectangle {
            id: themeButton
            anchors.right: parent.right
            anchors.rightMargin: g_settings.applyHScale(8)
            anchors.verticalCenter: parent.verticalCenter
            width: g_settings.applyHScale(62)
            height: g_settings.applyVScale(24)
            radius: height / 2
            color: themeMouse.containsMouse ?
                       (root.lightTheme ? "#c3e2ec" : "#205268") :
                       (root.lightTheme ? "#d4e7ee" : "#183b4c")
            border.color: root.lightTheme ? "#78aebe" : "#34758b"

            MyText {
                anchors.centerIn: parent
                text: root.lightTheme ? qsTr("浅色") : qsTr("深色")
                color: root.lightTheme ? "#244a5a" : "#d9f5fb"
                font.pixelSize: g_settings.fontPixelSizeSmall
            }

            MyMouseArea {
                id: themeMouse
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: root.lightTheme = !root.lightTheme
            }
        }
    }

    Canvas {
        id: targetCanvas
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.bottom: statusPanel.top
        anchors.leftMargin: g_settings.applyHScale(4)
        anchors.rightMargin: g_settings.applyHScale(4)
        anchors.topMargin: g_settings.applyVScale(4)
        anchors.bottomMargin: g_settings.applyVScale(4)

        onWidthChanged: requestPaint()
        onHeightChanged: requestPaint()

        onPaint: {
            var ctx = getContext("2d")
            ctx.clearRect(0, 0, width, height)

            var axisLeft = Math.max(42, g_settings.applyHScale(46))
            var axisRight = width - Math.max(8, g_settings.applyHScale(10))
            var axisTop = Math.max(8, g_settings.applyVScale(10))
            var axisBottom = height - Math.max(28, g_settings.applyVScale(32))
            var plotWidth = Math.max(1, axisRight - axisLeft)
            var plotHeight = Math.max(1, axisBottom - axisTop)
            var cx = axisLeft + plotWidth / 2
            var cy = axisTop + plotHeight / 2
            var shortSide = Math.min(plotWidth, plotHeight)
            var boundaryRadiusPx = shortSide * 0.35
            var diameter = Math.max(0.000001, root.boundaryDiameter)
            var pixelsPerUnit = boundaryRadiusPx * 2 / diameter
            var lineWidth = Math.max(1, g_settings.applyHScale(1))

            // 方形绘图区、细网格和外部坐标刻度。
            ctx.lineWidth = lineWidth
            ctx.strokeStyle = root.plotBorder
            ctx.fillStyle = root.plotBackground
            ctx.fillRect(axisLeft, axisTop, plotWidth, plotHeight)
            ctx.strokeRect(axisLeft, axisTop, plotWidth, plotHeight)

            ctx.font = Math.max(9, g_settings.applyFontScale(10)) + "px " + g_settings.fontFamilyNumber
            ctx.textAlign = "center"
            ctx.textBaseline = "top"
            for (var gridIndex = 0; gridIndex <= 10; ++gridIndex) {
                var gridX = axisLeft + plotWidth * gridIndex / 10
                var gridY = axisTop + plotHeight * gridIndex / 10

                ctx.strokeStyle = gridIndex === 5 ? root.gridCenter : root.gridNormal
                ctx.beginPath()
                ctx.moveTo(gridX, axisTop)
                ctx.lineTo(gridX, axisBottom)
                ctx.moveTo(axisLeft, gridY)
                ctx.lineTo(axisRight, gridY)
                ctx.stroke()

                ctx.strokeStyle = root.tickColor
                ctx.beginPath()
                ctx.moveTo(gridX, axisBottom)
                ctx.lineTo(gridX, axisBottom + g_settings.applyVScale(5))
                ctx.moveTo(axisLeft - g_settings.applyHScale(5), gridY)
                ctx.lineTo(axisLeft, gridY)
                ctx.stroke()

                ctx.fillStyle = root.secondaryText
                var xValue = (gridX - cx) / pixelsPerUnit
                var yValue = (cy - gridY) / pixelsPerUnit
                ctx.fillText(root.formatAxisValue(xValue), gridX,
                             axisBottom + g_settings.applyVScale(7))
                ctx.textAlign = "right"
                ctx.textBaseline = "middle"
                ctx.fillText(root.formatAxisValue(yValue),
                             axisLeft - g_settings.applyHScale(8), gridY)
                ctx.textAlign = "center"
                ctx.textBaseline = "top"
            }

            // 靶区层次环。
            ctx.fillStyle = root.lightTheme ?
                        "rgba(8, 135, 166, 0.06)" : "rgba(22, 103, 132, 0.08)"
            ctx.beginPath()
            ctx.arc(cx, cy, boundaryRadiusPx, 0, Math.PI * 2, false)
            ctx.fill()
            ctx.strokeStyle = root.lightTheme ?
                        "rgba(8, 120, 146, 0.25)" : "rgba(54, 206, 240, 0.28)"
            ctx.beginPath()
            ctx.arc(cx, cy, boundaryRadiusPx * 0.5, 0, Math.PI * 2, false)
            ctx.stroke()
            ctx.beginPath()
            ctx.arc(cx, cy, boundaryRadiusPx * 0.25, 0, Math.PI * 2, false)
            ctx.stroke()

            // 主边界圆。
            ctx.lineWidth = Math.max(1.2, g_settings.applyHScale(1.4))
            ctx.strokeStyle = root.targetColor

            ctx.beginPath()
            ctx.arc(cx, cy, boundaryRadiusPx, 0, Math.PI * 2, false)
            ctx.stroke()

            // 十字靶线延伸至圆周。
            ctx.lineWidth = lineWidth
            ctx.strokeStyle = root.targetBright
            ctx.beginPath()
            ctx.moveTo(cx - boundaryRadiusPx, cy)
            ctx.lineTo(cx + boundaryRadiusPx, cy)
            ctx.moveTo(cx, cy - boundaryRadiusPx)
            ctx.lineTo(cx, cy + boundaryRadiusPx)
            ctx.stroke()

            // 圆内十字轴：20 等分次刻度，10 等分主刻度，不显示数字。
            for (var tickIndex = -10; tickIndex <= 10; ++tickIndex) {
                if (tickIndex === 0)
                    continue
                var tickOffset = boundaryRadiusPx * tickIndex / 10
                var majorTick = tickIndex % 2 === 0
                var tickLength = majorTick ?
                            g_settings.applyHScale(7) : g_settings.applyHScale(4)
                ctx.lineWidth = majorTick ? Math.max(1.2, lineWidth) : lineWidth
                ctx.strokeStyle = majorTick ? root.targetTickMajor : root.targetTickMinor
                ctx.beginPath()
                ctx.moveTo(cx + tickOffset, cy - tickLength)
                ctx.lineTo(cx + tickOffset, cy + tickLength)
                ctx.moveTo(cx - tickLength, cy - tickOffset)
                ctx.lineTo(cx + tickLength, cy - tickOffset)
                ctx.stroke()
            }

            // 历史光斑：先绘制半透明光晕，再绘制红色核心。
            var pointRadius = Math.max(2, g_settings.applyHScale(2.5))
            for (var i = 0; i < root.pointHistory.length; ++i) {
                var point = root.pointHistory[i]
                var px = cx + point.x * pixelsPerUnit
                var py = cy - point.y * pixelsPerUnit

                if (px < axisLeft || px > axisRight ||
                        py < axisTop || py > axisBottom)
                    continue

                ctx.fillStyle = root.lightTheme ?
                            "rgba(220, 25, 45, 0.18)" : "rgba(255, 48, 58, 0.20)"
                ctx.beginPath()
                ctx.arc(px, py, pointRadius * 2.6, 0, Math.PI * 2, false)
                ctx.fill()
                ctx.fillStyle = root.lightTheme ? "#d91f35" : "#ff3344"
                ctx.beginPath()
                ctx.arc(px, py, pointRadius, 0, Math.PI * 2, false)
                ctx.fill()
            }
        }
    }

    Rectangle {
        id: statusPanel
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        anchors.margins: g_settings.applyHScale(7)
        height: g_settings.applyVScale(42)
        radius: g_settings.applyHScale(5)
        color: root.statusPanelColor
        border.color: root.panelBorder
        border.width: g_settings.applyHScale(1)

        Row {
            anchors.fill: parent
            anchors.margins: g_settings.applyHScale(3)
            spacing: g_settings.applyHScale(3)

            Repeater {
                model: 4

                Rectangle {
                    width: (statusPanel.width - g_settings.applyHScale(15)) / 4
                    height: parent.height
                    radius: g_settings.applyHScale(3)
                    color: index % 2 === 0 ? root.statusCardA : root.statusCardB

                    MyText {
                        anchors.centerIn: parent
                        color: index === 3 ? root.targetTickMajor : root.primaryText
                        font.pixelSize: g_settings.fontPixelSizeSmall
                        text: root.statusCardText(index)
                    }
                }
            }
        }
    }

    MyIconMenu {
        id: menu

        DeleteMenuItem {
            target: root
        }

        FillParentMenu {
            target: root
        }

        MyMenuItem {
            text_center: true
            text: root.lightTheme ? qsTr("切换为深色") : qsTr("切换为浅色")
            checked: root.lightTheme
            onTriggered: root.lightTheme = !root.lightTheme
        }

        SettingsMenu {
            title: qsTr("脱靶量设置")

            MyMenuItem {
                text_center: true
                text: qsTr("边界圆直径") + ":"
                plus_minus_on: true
                value_text: "" + root.boundaryDiameter
                value_editable: true
                onPlus_triggered: root.setBoundaryDiameter(root.boundaryDiameter + 1)
                onMinus_triggered: root.setBoundaryDiameter(root.boundaryDiameter - 1)
                onValue_inputed: root.setBoundaryDiameter(parseFloat(text))
            }

            MyMenuItem {
                text_center: true
                text: qsTr("历史点上限") + ":"
                plus_minus_on: true
                value_text: "" + root.historyLimit
                value_editable: true
                onPlus_triggered: root.setHistoryLimit(root.historyLimit + 100)
                onMinus_triggered: root.setHistoryLimit(root.historyLimit - 100)
                onValue_inputed: root.setHistoryLimit(parseInt(text))
            }

            MyMenuSeparator { }

            MyMenuItem {
                text_center: true
                text: qsTr("清空历史点")
                onTriggered: root.clearHistory()
            }
        }

        MyMenuSeparator { }

        ChMenu {
            id: track_x_menu
            checked: bind_obj
            color: "#ff9800"
            indicator_color: bind_obj ? bind_obj.color : color
            title: qsTr("跟踪点 X") + (bind_obj ? (" → " + bind_obj.name) : "")
            onBind_objChanged: root.resetSampling(true)
        }

        ChMenu {
            id: track_y_menu
            checked: bind_obj
            color: "#ffc107"
            indicator_color: bind_obj ? bind_obj.color : color
            title: qsTr("跟踪点 Y") + (bind_obj ? (" → " + bind_obj.name) : "")
            onBind_objChanged: root.resetSampling(true)
        }

        ChMenu {
            id: spot_x_menu
            checked: bind_obj
            color: "#f44336"
            indicator_color: bind_obj ? bind_obj.color : color
            title: qsTr("光斑 X") + (bind_obj ? (" → " + bind_obj.name) : "")
            onBind_objChanged: root.resetSampling(true)
        }

        ChMenu {
            id: spot_y_menu
            checked: bind_obj
            color: "#e91e63"
            indicator_color: bind_obj ? bind_obj.color : color
            title: qsTr("光斑 Y") + (bind_obj ? (" → " + bind_obj.name) : "")
            onBind_objChanged: root.resetSampling(true)
        }
    }

    Connections {
        target: root.mouse

        onClicked: {
            if (mouse.button === Qt.RightButton)
                menu.popup()
        }
    }

    Connections {
        target: sys_manager
        onNeed_update: root.captureSample()
    }

    onBoundaryDiameterChanged: targetCanvas.requestPaint()
    onLightThemeChanged: targetCanvas.requestPaint()
    onHistoryLimitChanged: {
        if (pointHistory.length > historyLimit)
            clearHistory()
    }

    function setBoundaryDiameter(value) {
        if (!isFinite(value))
            return
        boundaryDiameter = Math.max(0.000001, value)
    }

    function setHistoryLimit(value) {
        if (!isFinite(value))
            return
        historyLimit = Math.max(1, Math.round(value))
    }

    function clearHistory() {
        pointHistory = []
        targetCanvas.requestPaint()
    }

    function resetSampling(clearPoints) {
        sampleInitialized = false
        hasLatestSample = false
        if (clearPoints)
            clearHistory()
    }

    function formatAxisValue(value) {
        if (!isFinite(value) || Math.abs(value) < 0.000001)
            return "0"
        if (Math.abs(value - Math.round(value)) < 0.000001)
            return "" + Math.round(value)
        return "" + Number(value.toFixed(2))
    }

    function statusCardText(index) {
        if (index === 3)
            return qsTr("点数") + "\n" + pointHistory.length + " / " + historyLimit
        if (!hasLatestSample) {
            if (index === 0)
                return "ΔX\n--"
            if (index === 1)
                return "ΔY\n--"
            return "R\n--"
        }
        if (index === 0)
            return "ΔX\n" + formatAxisValue(latestDeltaX)
        if (index === 1)
            return "ΔY\n" + formatAxisValue(latestDeltaY)
        return "R\n" + formatAxisValue(latestRadius)
    }

    function channelsBound() {
        return track_x_menu.bind_obj && track_y_menu.bind_obj &&
                spot_x_menu.bind_obj && spot_y_menu.bind_obj
    }

    function captureSample() {
        if (!channelsBound())
            return

        var trackX = Number(track_x_menu.bind_obj.value)
        var trackY = Number(track_y_menu.bind_obj.value)
        var spotX = Number(spot_x_menu.bind_obj.value)
        var spotY = Number(spot_y_menu.bind_obj.value)
        if (!isFinite(trackX) || !isFinite(trackY) ||
                !isFinite(spotX) || !isFinite(spotY))
            return

        var changed = !sampleInitialized ||
                trackX !== lastTrackX || trackY !== lastTrackY ||
                spotX !== lastSpotX || spotY !== lastSpotY

        lastTrackX = trackX
        lastTrackY = trackY
        lastSpotX = spotX
        lastSpotY = spotY

        if (!changed) {
            sampleInitialized = true
            return
        }

        if (pointHistory.length >= historyLimit)
            pointHistory = []

        var nextHistory = pointHistory.slice(0)
        latestDeltaX = spotX - trackX
        latestDeltaY = spotY - trackY
        latestRadius = Math.sqrt(latestDeltaX * latestDeltaX +
                                 latestDeltaY * latestDeltaY)
        hasLatestSample = true
        nextHistory.push({ "x": latestDeltaX, "y": latestDeltaY })
        pointHistory = nextHistory
        sampleInitialized = true
        targetCanvas.requestPaint()
    }

    function get_widget_ctx() {
        return {
            "path": path,
            "ctx": {
                ".": {
                    "ctx": get_ctx(),
                    "boundaryDiameter": boundaryDiameter,
                    "historyLimit": historyLimit,
                    "lightTheme": lightTheme
                },
                "track_x_menu": { "ctx": track_x_menu.get_ctx() },
                "track_y_menu": { "ctx": track_y_menu.get_ctx() },
                "spot_x_menu": { "ctx": spot_x_menu.get_ctx() },
                "spot_y_menu": { "ctx": spot_y_menu.get_ctx() }
            }
        }
    }

    function set_widget_ctx(ctx) {
        clearHistory()
        __set_ctx__(root, ctx.ctx, ref)
        setBoundaryDiameter(boundaryDiameter)
        setHistoryLimit(historyLimit)
        sampleInitialized = false
        targetCanvas.requestPaint()
    }
}
