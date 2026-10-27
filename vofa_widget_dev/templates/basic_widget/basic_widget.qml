import QtQuick 2.12
import MyModules 1.0

ResizableRectangle {
    id: root

    // 文件夹名、入口文件名和 path 必须一致。
    property string path: "basic_widget"

    width: g_settings.applyHScale(160)
    height: g_settings.applyVScale(100)
    minimumWidth: g_settings.applyHScale(100)
    minimumHeight: g_settings.applyVScale(60)
    color: appTheme.bgColor
    border.color: appTheme.lineColor
    border.width: g_settings.applyHScale(1)

    MyText {
        anchors.centerIn: parent
        text: root.path
        color: appTheme.fontColor
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
