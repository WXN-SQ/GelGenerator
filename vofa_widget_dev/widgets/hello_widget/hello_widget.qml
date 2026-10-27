import QtQuick 2.12
import MyModules 1.0

ResizableRectangle {
    id: root

    property string path: "hello_widget"

    width: g_settings.applyHScale(160)
    height: g_settings.applyVScale(100)
    minimumWidth: g_settings.applyHScale(120)
    minimumHeight: g_settings.applyVScale(72)
    radius: g_settings.applyHScale(6)
    color: appTheme.bgColor
    border.color: appTheme.lineColor
    border.width: g_settings.applyHScale(1)

    MyText {
        anchors.centerIn: parent
        text: qsTr("Hello VOFA+")
        color: appTheme.fontColor
        font.pixelSize: g_settings.fontPixelSizeNormal
    }

    MyMenu {
        id: menu

        DeleteMenuItem {
            target: root
        }
    }

    Connections {
        target: root.mouse

        onClicked: {
            if (mouse.button === Qt.RightButton)
                menu.popup()
        }
    }

    function get_widget_ctx() {
        return {
            "path": path,
            "ctx": {
                ".": { "ctx": get_ctx() }
            }
        }
    }

    function set_widget_ctx(ctx) {
        __set_ctx__(root, ctx.ctx)
    }
}
