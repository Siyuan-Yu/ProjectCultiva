using System.Text;
using System.IO;
using System.Windows;

namespace SurfaceAuthoring.EditorCommon;

public static class EditorCrashReporter
{
    private static readonly object Gate=new();
    private static bool _reporting;

    public static void Report(string editorName,Exception exception)
    {
        lock(Gate){if(_reporting)return;_reporting=true;}
        try
        {
            var path=Path.Combine(AppContext.BaseDirectory,editorName+"-crash.log");var text=new StringBuilder().AppendLine("时间："+DateTimeOffset.Now.ToString("O"));var current=exception;var depth=0;
            while(current!=null){text.AppendLine().AppendLine(depth==0?"异常：":"内部异常：").AppendLine("类型："+current.GetType().FullName).AppendLine("消息："+current.Message).AppendLine("堆栈：").AppendLine(current.StackTrace??"（无堆栈信息）");current=current.InnerException!;depth++;}
            try{File.WriteAllText(path,text.ToString(),new UTF8Encoding(false));}catch{}
            try{MessageBox.Show("编辑器发生未处理错误，详细信息已写入日志。",editorName+" · 启动失败",MessageBoxButton.OK,MessageBoxImage.Error);}catch{}
        }
        finally{lock(Gate)_reporting=false;}
    }
}
