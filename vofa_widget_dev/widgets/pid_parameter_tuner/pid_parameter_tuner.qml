import QtQuick 2.12
import QtQuick.Controls 2.12
import MyModules 1.0

ResizableRectangle {
    id: root

    property string path: "pid_parameter_tuner"
    property string titleText: qsTr("PID 参数调节")
    property bool lightTheme: false
    property bool titleEditing: false
    readonly property color panelBase: lightTheme ? "#eef4f7" : "#0e1620"
    readonly property color panelBorder: lightTheme ? "#9cb8c5" : "#345267"
    readonly property color gradientTop: lightTheme ? "#ffffff" : "#1a2937"
    readonly property color gradientMiddle: lightTheme ? "#edf4f7" : "#101a25"
    readonly property color gradientBottom: lightTheme ? "#dfe9ee" : "#0a1119"
    readonly property color headerColor: lightTheme ? "#e4eff4" : "#142532"
    readonly property color dividerColor: lightTheme ? "#a7c3cf" : "#315064"
    readonly property color primaryText: lightTheme ? "#17313e" : "#e2f2f8"
    readonly property color secondaryText: lightTheme ? "#537687" : "#6f98aa"
    readonly property color rowColorA: lightTheme ? "#f7fbfc" : "#172d3b"
    readonly property color rowColorB: lightTheme ? "#edf5f8" : "#172a38"
    readonly property color rowColorC: lightTheme ? "#f4f7f8" : "#152735"
    readonly property color rowBorder: lightTheme ? "#aac4cf" : "#294858"
    readonly property color inputColor: lightTheme ? "#ffffff" : "#0c1821"
    readonly property color inputFocusColor: lightTheme ? "#f5fdff" : "#102633"
    readonly property color buttonColor: lightTheme ? "#dceaf0" : "#192f3d"
    readonly property color buttonHoverColor: lightTheme ? "#c6e1eb" : "#203f51"
    readonly property color actionPanelColor: lightTheme ? "#e6f0f4" : "#122531"
    property real kpValue: 0
    property real kiValue: 0
    property real kdValue: 0
    property real kpMin: -50
    property real kiMin: -50
    property real kdMin: -50
    property real kpMax: 50
    property real kiMax: 50
    property real kdMax: 50
    property real kpStep: 0.01
    property real kiStep: 0.01
    property real kdStep: 0.01
    property int selectedSlot: 0
    property var savedSlots: [null, null, null, null, null,
                              null, null, null, null, null]
    property string savedSlotsJson: "[]"
    signal valuesUpdated()

    property Item ref: Loader {
        active: false
        sourceComponent: Component {
            Item {
                property var ref_kp_cmd_menu: kp_cmd_menu
                property var ref_ki_cmd_menu: ki_cmd_menu
                property var ref_kd_cmd_menu: kd_cmd_menu
            }
        }
    }

    width: g_settings.applyHScale(540)
    height: g_settings.applyVScale(350)
    minimumWidth: g_settings.applyHScale(440)
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
            GradientStop { position: 0.55; color: root.gradientMiddle }
            GradientStop { position: 1.0; color: root.gradientBottom }
        }
    }

    Item {
        id: header
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        height: g_settings.applyVScale(48)

        Rectangle {
            anchors.fill: parent
            color: root.headerColor
            opacity: 0.92
            radius: root.radius
        }

        Rectangle {
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            height: g_settings.applyVScale(1)
            color: root.dividerColor
        }

        Rectangle {
            anchors.left: parent.left
            anchors.leftMargin: g_settings.applyHScale(12)
            anchors.verticalCenter: parent.verticalCenter
            width: g_settings.applyHScale(5)
            height: g_settings.applyVScale(24)
            radius: width / 2
            color: root.lightTheme ? "#1597b5" : "#34c9ed"
        }

        Item {
            id: titleArea
            anchors.left: parent.left
            anchors.leftMargin: g_settings.applyHScale(28)
            anchors.right: themeButton.left
            anchors.rightMargin: g_settings.applyHScale(96)
            anchors.verticalCenter: parent.verticalCenter
            height: g_settings.applyVScale(34)

            MyText {
                anchors.fill: parent
                verticalAlignment: Text.AlignVCenter
                text: root.titleText
                color: root.primaryText
                font.pixelSize: g_settings.fontPixelSizeBig
                elide: Text.ElideRight
                visible: !root.titleEditing
            }

            MyMouseArea {
                anchors.fill: parent
                enabled: !root.titleEditing
                cursorShape: Qt.IBeamCursor
                onDoubleClicked: {
                    root.titleEditing = true
                    titleInput.text = root.titleText
                    titleInput.forceActiveFocus()
                    titleInput.selectAll()
                }
            }

            TextField {
                id: titleInput
                anchors.fill: parent
                visible: root.titleEditing
                color: root.primaryText
                selectionColor: "#239aba"
                selectedTextColor: "white"
                font.pixelSize: g_settings.fontPixelSizeBig
                verticalAlignment: TextInput.AlignVCenter
                selectByMouse: true
                background: Rectangle {
                    color: "transparent"
                    border.color: "#31b9d9"
                    border.width: g_settings.applyHScale(1)
                    radius: g_settings.applyHScale(3)
                }
                onAccepted: root.finishTitleEditing(text)
                onActiveFocusChanged: {
                    if (!activeFocus && root.titleEditing)
                        root.finishTitleEditing(text)
                }
            }
        }

        MyText {
            anchors.right: themeButton.left
            anchors.rightMargin: g_settings.applyHScale(10)
            anchors.verticalCenter: parent.verticalCenter
            text: qsTr("FLOAT CONTROL")
            color: root.secondaryText
            font.pixelSize: g_settings.fontPixelSizeSmall
        }

        Rectangle {
            id: themeButton
            anchors.right: parent.right
            anchors.rightMargin: g_settings.applyHScale(10)
            anchors.verticalCenter: parent.verticalCenter
            width: g_settings.applyHScale(72)
            height: g_settings.applyVScale(28)
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

    Column {
        id: parameterColumn
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.leftMargin: g_settings.applyHScale(12)
        anchors.rightMargin: g_settings.applyHScale(12)
        anchors.topMargin: g_settings.applyVScale(10)
        spacing: g_settings.applyVScale(8)

        Repeater {
            model: 3

            Rectangle {
                id: parameterRow
                width: parameterColumn.width
                height: g_settings.applyVScale(58)
                radius: g_settings.applyHScale(6)
                color: index === 0 ? root.rowColorA :
                                     (index === 1 ? root.rowColorB : root.rowColorC)
                border.color: valueInput.activeFocus ? "#38cbed" : root.rowBorder
                border.width: g_settings.applyHScale(1)

                Rectangle {
                    anchors.left: parent.left
                    anchors.top: parent.top
                    anchors.bottom: parent.bottom
                    width: g_settings.applyHScale(5)
                    radius: parent.radius
                    color: root.parameterColor(index)
                }

                MyText {
                    anchors.left: parent.left
                    anchors.leftMargin: g_settings.applyHScale(18)
                    anchors.verticalCenter: parent.verticalCenter
                    width: g_settings.applyHScale(34)
                    text: root.parameterName(index)
                    color: root.parameterColor(index)
                    font.bold: true
                    font.pixelSize: g_settings.fontPixelSizeBig
                }

                Rectangle {
                    id: minusButton
                    anchors.left: parent.left
                    anchors.leftMargin: g_settings.applyHScale(64)
                    anchors.verticalCenter: parent.verticalCenter
                    width: g_settings.applyHScale(42)
                    height: g_settings.applyVScale(38)
                    radius: g_settings.applyHScale(5)
                    color: minusMouse.pressed ?
                               (root.lightTheme ? "#b4d5e0" : "#245269") :
                               (minusMouse.containsMouse ? root.buttonHoverColor : root.buttonColor)
                    border.color: root.lightTheme ? "#8eb5c3" : "#396276"

                    MyText {
                        anchors.centerIn: parent
                        text: "−"
                        color: root.primaryText
                        font.pixelSize: g_settings.fontPixelSizeBig
                    }

                    MyMouseArea {
                        id: minusMouse
                        anchors.fill: parent
                        hoverEnabled: true
                        cursorShape: Qt.PointingHandCursor
                        onClicked: root.adjustParameter(index, -1)
                    }
                }

                TextField {
                    id: valueInput
                    anchors.left: minusButton.right
                    anchors.leftMargin: g_settings.applyHScale(8)
                    anchors.verticalCenter: parent.verticalCenter
                    width: Math.max(g_settings.applyHScale(130),
                                    parent.width - g_settings.applyHScale(302))
                    height: g_settings.applyVScale(38)
                    text: root.formatValue(root.parameterValue(index))
                    color: root.primaryText
                    selectionColor: "#237d99"
                    selectedTextColor: "white"
                    horizontalAlignment: TextInput.AlignHCenter
                    verticalAlignment: TextInput.AlignVCenter
                    font.family: g_settings.fontFamilyNumber
                    font.pixelSize: g_settings.fontPixelSizeNormal
                    selectByMouse: true
                    validator: DoubleValidator {
                        bottom: root.parameterMin(index)
                        top: root.parameterMax(index)
                        decimals: 8
                    }
                    background: Rectangle {
                        radius: g_settings.applyHScale(4)
                        color: valueInput.activeFocus ? root.inputFocusColor : root.inputColor
                        border.color: valueInput.activeFocus ? "#42b7d2" : root.dividerColor
                    }
                    onAccepted: {
                        root.submitParameter(index, text)
                        text = root.formatValue(root.parameterValue(index))
                        focus = false
                    }
                    onActiveFocusChanged: {
                        if (!activeFocus)
                            text = root.formatValue(root.parameterValue(index))
                    }

                    Connections {
                        target: root
                        onValuesUpdated: {
                            if (!valueInput.activeFocus)
                                valueInput.text = root.formatValue(root.parameterValue(index))
                        }
                    }
                }

                Rectangle {
                    id: plusButton
                    anchors.left: valueInput.right
                    anchors.leftMargin: g_settings.applyHScale(8)
                    anchors.verticalCenter: parent.verticalCenter
                    width: g_settings.applyHScale(42)
                    height: g_settings.applyVScale(38)
                    radius: g_settings.applyHScale(5)
                    color: plusMouse.pressed ?
                               (root.lightTheme ? "#a8d6e3" : "#166a80") :
                               (plusMouse.containsMouse ?
                                    (root.lightTheme ? "#c4e5ed" : "#18566b") :
                                    (root.lightTheme ? "#d7edf2" : "#173847"))
                    border.color: root.lightTheme ? "#79adbc" : "#39758a"

                    MyText {
                        anchors.centerIn: parent
                        text: "+"
                        color: root.lightTheme ? "#17495a" : "#dff9ff"
                        font.pixelSize: g_settings.fontPixelSizeBig
                    }

                    MyMouseArea {
                        id: plusMouse
                        anchors.fill: parent
                        hoverEnabled: true
                        cursorShape: Qt.PointingHandCursor
                        onClicked: root.adjustParameter(index, 1)
                    }
                }

                Rectangle {
                    anchors.left: plusButton.right
                    anchors.leftMargin: g_settings.applyHScale(8)
                    anchors.right: parent.right
                    anchors.rightMargin: g_settings.applyHScale(10)
                    anchors.verticalCenter: parent.verticalCenter
                    height: g_settings.applyVScale(38)
                    radius: g_settings.applyHScale(5)
                    color: submitMouse.containsMouse ?
                               (root.lightTheme ? "#b8dfe9" : "#176a82") :
                               (root.lightTheme ? "#d1e9ef" : "#174357")
                    border.color: root.lightTheme ? "#6da5b6" : "#3187a1"

                    MyText {
                        anchors.centerIn: parent
                        text: qsTr("提交")
                        color: root.lightTheme ? "#17495a" : "#dff8ff"
                        font.pixelSize: g_settings.fontPixelSizeSmall
                    }

                    MyMouseArea {
                        id: submitMouse
                        anchors.fill: parent
                        hoverEnabled: true
                        cursorShape: Qt.PointingHandCursor
                        onClicked: {
                            root.submitParameter(index, valueInput.text)
                            valueInput.text = root.formatValue(root.parameterValue(index))
                        }
                    }
                }

            }
        }
    }

    Rectangle {
        id: actionPanel
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        anchors.leftMargin: g_settings.applyHScale(12)
        anchors.rightMargin: g_settings.applyHScale(12)
        anchors.bottomMargin: g_settings.applyVScale(10)
        height: g_settings.applyVScale(68)
        radius: g_settings.applyHScale(6)
        color: root.actionPanelColor
        border.color: root.rowBorder

        Row {
            anchors.centerIn: parent
            spacing: g_settings.applyHScale(7)

            Repeater {
                model: ["slotDown", "slot", "slotUp", "save", "restore", "sendAll"]

                Rectangle {
                    width: modelData === "slot" ? g_settings.applyHScale(90) :
                           (modelData === "sendAll" ? g_settings.applyHScale(82) :
                            (modelData === "slotDown" || modelData === "slotUp" ?
                                 g_settings.applyHScale(40) : g_settings.applyHScale(56)))
                    height: g_settings.applyVScale(38)
                    radius: g_settings.applyHScale(5)
                    color: actionMouse.containsMouse ?
                               (modelData === "sendAll" ?
                                    (root.lightTheme ? "#a9dce8" : "#167d9b") :
                                    root.buttonHoverColor) :
                               (modelData === "sendAll" ?
                                    (root.lightTheme ? "#c9e7ee" : "#17556b") :
                                    root.buttonColor)
                    border.color: modelData === "sendAll" ?
                                      (root.lightTheme ? "#5fa5b7" : "#38b6d4") :
                                      root.rowBorder

                    MyText {
                        anchors.centerIn: parent
                        horizontalAlignment: Text.AlignHCenter
                        text: root.actionText(modelData)
                        color: modelData === "slot" ?
                                   (root.lightTheme ? "#087c99" : "#6fdcf4") :
                                   root.primaryText
                        font.pixelSize: g_settings.fontPixelSizeSmall
                    }

                    MyMouseArea {
                        id: actionMouse
                        anchors.fill: parent
                        enabled: modelData !== "slot"
                        hoverEnabled: enabled
                        cursorShape: enabled ? Qt.PointingHandCursor : Qt.ArrowCursor
                        onClicked: root.runAction(modelData)
                    }
                }
            }
        }
    }

    MyIconMenu {
        id: menu

        DeleteMenuItem { target: root }
        FillParentMenu { target: root }

        MyMenuItem {
            text_center: true
            text: root.lightTheme ? qsTr("切换为深色") : qsTr("切换为浅色")
            checked: root.lightTheme
            onTriggered: root.lightTheme = !root.lightTheme
        }

        SettingsMenu {
            title: qsTr("参数设置")

            MyMenu {
                title: "Kp"
                MyMenuItem {
                    text_center: true; text: qsTr("最小值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kpMin)
                    onPlus_triggered: root.setParameterMin(0, root.kpMin + 1)
                    onMinus_triggered: root.setParameterMin(0, root.kpMin - 1)
                    onValue_inputed: root.setParameterMin(0, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("最大值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kpMax)
                    onPlus_triggered: root.setParameterMax(0, root.kpMax + 1)
                    onMinus_triggered: root.setParameterMax(0, root.kpMax - 1)
                    onValue_inputed: root.setParameterMax(0, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("微调步进") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kpStep)
                    onPlus_triggered: root.setParameterStep(0, root.kpStep * 10)
                    onMinus_triggered: root.setParameterStep(0, root.kpStep / 10)
                    onValue_inputed: root.setParameterStep(0, parseFloat(text))
                }
            }
            MyMenu {
                title: "Ki"
                MyMenuItem {
                    text_center: true; text: qsTr("最小值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kiMin)
                    onPlus_triggered: root.setParameterMin(1, root.kiMin + 1)
                    onMinus_triggered: root.setParameterMin(1, root.kiMin - 1)
                    onValue_inputed: root.setParameterMin(1, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("最大值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kiMax)
                    onPlus_triggered: root.setParameterMax(1, root.kiMax + 1)
                    onMinus_triggered: root.setParameterMax(1, root.kiMax - 1)
                    onValue_inputed: root.setParameterMax(1, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("微调步进") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kiStep)
                    onPlus_triggered: root.setParameterStep(1, root.kiStep * 10)
                    onMinus_triggered: root.setParameterStep(1, root.kiStep / 10)
                    onValue_inputed: root.setParameterStep(1, parseFloat(text))
                }
            }
            MyMenu {
                title: "Kd"
                MyMenuItem {
                    text_center: true; text: qsTr("最小值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kdMin)
                    onPlus_triggered: root.setParameterMin(2, root.kdMin + 1)
                    onMinus_triggered: root.setParameterMin(2, root.kdMin - 1)
                    onValue_inputed: root.setParameterMin(2, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("最大值") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kdMax)
                    onPlus_triggered: root.setParameterMax(2, root.kdMax + 1)
                    onMinus_triggered: root.setParameterMax(2, root.kdMax - 1)
                    onValue_inputed: root.setParameterMax(2, parseFloat(text))
                }
                MyMenuItem {
                    text_center: true; text: qsTr("微调步进") + ":"
                    plus_minus_on: true; value_editable: true
                    value_text: root.formatValue(root.kdStep)
                    onPlus_triggered: root.setParameterStep(2, root.kdStep * 10)
                    onMinus_triggered: root.setParameterStep(2, root.kdStep / 10)
                    onValue_inputed: root.setParameterStep(2, parseFloat(text))
                }
            }
        }

        MyMenuSeparator { }

        MyMenu {
            title: qsTr("输出命令（浮点）")
            CmdMenu {
                id: kp_cmd_menu
                checked: bind_obj
                title: "Kp" + (bind_obj ? (" → " + bind_obj.name) : "")
            }
            CmdMenu {
                id: ki_cmd_menu
                checked: bind_obj
                title: "Ki" + (bind_obj ? (" → " + bind_obj.name) : "")
            }
            CmdMenu {
                id: kd_cmd_menu
                checked: bind_obj
                title: "Kd" + (bind_obj ? (" → " + bind_obj.name) : "")
            }
        }

        MyMenuSeparator { }

        MyMenuItem {
            text_center: true
            text: qsTr("一键全部发送")
            onTriggered: root.sendAll()
        }
    }

    Connections {
        target: root.mouse
        onClicked: {
            if (mouse.button === Qt.RightButton)
                menu.popup()
        }
    }

    function parameterName(index) {
        return index === 0 ? "Kp" : (index === 1 ? "Ki" : "Kd")
    }

    function parameterColor(index) {
        if (lightTheme)
            return index === 0 ? "#087f9d" : (index === 1 ? "#168762" : "#a96908")
        return index === 0 ? "#3fd3f2" : (index === 1 ? "#68dfb2" : "#f2ba55")
    }

    function finishTitleEditing(value) {
        var trimmed = ("" + value).trim()
        titleText = trimmed.length > 0 ? trimmed : qsTr("PID 参数调节")
        titleEditing = false
    }

    function parameterValue(index) {
        return index === 0 ? kpValue : (index === 1 ? kiValue : kdValue)
    }

    function parameterMin(index) {
        return index === 0 ? kpMin : (index === 1 ? kiMin : kdMin)
    }

    function parameterMax(index) {
        return index === 0 ? kpMax : (index === 1 ? kiMax : kdMax)
    }

    function parameterStep(index) {
        return index === 0 ? kpStep : (index === 1 ? kiStep : kdStep)
    }

    function commandMenu(index) {
        return index === 0 ? kp_cmd_menu : (index === 1 ? ki_cmd_menu : kd_cmd_menu)
    }

    function setParameterValue(index, value, applyClamp) {
        if (applyClamp === undefined || applyClamp)
            value = clamp(index, value)
        else if (!isFinite(value))
            return parameterValue(index)
        if (index === 0)
            kpValue = value
        else if (index === 1)
            kiValue = value
        else
            kdValue = value
        valuesUpdated()
        return value
    }

    function setParameterMin(index, value) {
        if (!isFinite(value))
            return
        if (index === 0) kpMin = Math.min(value, kpMax)
        else if (index === 1) kiMin = Math.min(value, kiMax)
        else kdMin = Math.min(value, kdMax)
        setParameterValue(index, parameterValue(index))
    }

    function setParameterMax(index, value) {
        if (!isFinite(value))
            return
        if (index === 0) kpMax = Math.max(value, kpMin)
        else if (index === 1) kiMax = Math.max(value, kiMin)
        else kdMax = Math.max(value, kdMin)
        setParameterValue(index, parameterValue(index))
    }

    function setParameterStep(index, value) {
        if (!isFinite(value) || value <= 0)
            return
        if (index === 0) kpStep = value
        else if (index === 1) kiStep = value
        else kdStep = value
        valuesUpdated()
    }

    function clamp(index, value) {
        if (!isFinite(value))
            value = parameterValue(index)
        return Math.max(parameterMin(index), Math.min(parameterMax(index), value))
    }

    function formatValue(value) {
        if (!isFinite(value) || Math.abs(value) < 0.000000001)
            return "0"
        return "" + Number(value.toFixed(8))
    }

    function adjustParameter(index, direction) {
        var value = parameterValue(index) + direction * parameterStep(index)
        value = setParameterValue(index, value)
        sendParameter(index, value)
    }

    function submitParameter(index, text) {
        var value = parseFloat(text)
        if (!isFinite(value)) {
            valuesUpdated()
            return
        }
        value = setParameterValue(index, value)
        sendParameter(index, value)
    }

    function sendParameter(index, value) {
        var cmd = commandMenu(index)
        if (!cmd.bind_obj || !sys_manager.connected)
            return
        value = clamp(index, value)
        var argument = {
            "name": parameterName(index),
            "float_value": value,
            "hex_value": sys_manager.float_to_hex(value),
            "enabled": true,
            "changable": false
        }
        sys_manager.send_command(parameterName(index), cmd.bind_obj, argument, false)
    }

    function sendAll() {
        sendParameter(0, parameterValue(0))
        sendParameter(1, parameterValue(1))
        sendParameter(2, parameterValue(2))
    }

    function actionText(action) {
        if (action === "slotDown") return "◀"
        if (action === "slotUp") return "▶"
        if (action === "save") return qsTr("保存")
        if (action === "restore") return qsTr("恢复")
        if (action === "sendAll") return qsTr("全部发送")
        return qsTr("参数组") + " " + (selectedSlot + 1) + "\n" +
                (savedSlots[selectedSlot] ? qsTr("已保存") : qsTr("空"))
    }

    function runAction(action) {
        if (action === "slotDown")
            selectedSlot = (selectedSlot + 9) % 10
        else if (action === "slotUp")
            selectedSlot = (selectedSlot + 1) % 10
        else if (action === "save")
            saveCurrentSlot()
        else if (action === "restore")
            restoreCurrentSlot()
        else if (action === "sendAll")
            sendAll()
    }

    function saveCurrentSlot() {
        var slots = savedSlots.slice(0)
        slots[selectedSlot] = {
            "kp": parameterValue(0),
            "ki": parameterValue(1),
            "kd": parameterValue(2)
        }
        savedSlots = slots
        savedSlotsJson = JSON.stringify(savedSlots)
    }

    function restoreCurrentSlot() {
        var slot = savedSlots[selectedSlot]
        if (!slot)
            return
        setParameterValue(0, Number(slot.kp))
        setParameterValue(1, Number(slot.ki))
        setParameterValue(2, Number(slot.kd))
        sendAll()
    }

    function parseSavedSlots() {
        try {
            var parsed = JSON.parse(savedSlotsJson)
            if (!(parsed instanceof Array))
                parsed = []
            while (parsed.length < 10)
                parsed.push(null)
            savedSlots = parsed.slice(0, 10)
        } catch (error) {
            savedSlots = [null, null, null, null, null,
                          null, null, null, null, null]
        }
    }

    function get_widget_ctx() {
        savedSlotsJson = JSON.stringify(savedSlots)
        return {
            "path": path,
            "ctx": {
                ".": {
                    "ctx": get_ctx(),
                    "kpValue": kpValue, "kiValue": kiValue, "kdValue": kdValue,
                    "kpMin": kpMin, "kiMin": kiMin, "kdMin": kdMin,
                    "kpMax": kpMax, "kiMax": kiMax, "kdMax": kdMax,
                    "kpStep": kpStep, "kiStep": kiStep, "kdStep": kdStep,
                    "selectedSlot": selectedSlot,
                    "savedSlotsJson": savedSlotsJson,
                    "titleText": titleText,
                    "lightTheme": lightTheme
                },
                "kp_cmd_menu": { "ctx": kp_cmd_menu.get_ctx() },
                "ki_cmd_menu": { "ctx": ki_cmd_menu.get_ctx() },
                "kd_cmd_menu": { "ctx": kd_cmd_menu.get_ctx() }
            }
        }
    }

    function set_widget_ctx(ctx) {
        __set_ctx__(root, ctx.ctx, ref)
        selectedSlot = Math.max(0, Math.min(9, selectedSlot))
        parseSavedSlots()
        setParameterMin(0, kpMin); setParameterMin(1, kiMin); setParameterMin(2, kdMin)
        setParameterMax(0, kpMax); setParameterMax(1, kiMax); setParameterMax(2, kdMax)
        setParameterStep(0, kpStep); setParameterStep(1, kiStep); setParameterStep(2, kdStep)
        valuesUpdated()
    }
}
