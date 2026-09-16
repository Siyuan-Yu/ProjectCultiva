using Microsoft.Win32;
using SurfaceAuthoring.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FineEditor;

public partial class MainWindow:Window
{
    private static readonly Choice<BaseTerrain>[] BaseChoices=[new(BaseTerrain.Plain,"平原"),new(BaseTerrain.Mountain,"山地"),new(BaseTerrain.Water,"水域")];
    private static readonly Choice<TerrainFeature>[] FeatureChoices=[new(TerrainFeature.None,"无"),new(TerrainFeature.Forest,"森林")];
    private readonly FineEditorCanvas _canvas=new();
    private readonly TextBlock _info=new(),_status=new(),_help=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray};
    private readonly ComboBox _base=Combo(BaseChoices),_feature=Combo(FeatureChoices),_brush=new(){ItemsSource=new[]{1,2,4,8}};
    private readonly Slider _density=new(){Minimum=0,Maximum=1,Value=.75,TickFrequency=.1};
    private readonly Stack<string> _undo=[],_redo=[];
    private object _document=new WorldSiteBlueprintDocument{BlueprintId="new_blueprint",DisplayName="新据点蓝图",WidthCells=37,HeightCells=23};
    private string? _path,_snapshot;
    private bool _dirty;
    private bool _startupInitialized;
    private readonly TextBox _kind=new(),_contentRef=new(),_assetRef=new(),_x=new(),_y=new(),_w=new(),_h=new();

    public MainWindow()
    {
        InitializeComponent();Content=BuildUi();_base.SelectedValue=BaseTerrain.Plain;_feature.SelectedValue=TerrainFeature.Forest;_brush.SelectedItem=1;
        _base.SelectionChanged+=(_,_)=>Sync();_feature.SelectionChanged+=(_,_)=>Sync();_brush.SelectionChanged+=(_,_)=>Sync();_density.ValueChanged+=(_,_)=>Sync();
        _canvas.EditStarted+=()=>_snapshot=Serialize();_canvas.EditCompleted+=()=>{var next=Serialize();if(_snapshot!=null&&_snapshot!=next){_undo.Push(_snapshot);_redo.Clear();Dirty();}_snapshot=null;Refresh();};
        _canvas.ObjectSelectionChanged+=LoadInspector;
        _canvas.UserNotice+=message=>_status.Text=message;
        _canvas.CursorCellChanged+=(x,y)=>_status.Text=x<0?"不在文档范围内":$"连续世界格：({x}, {y}) · 当前工具：{ToolText(_canvas.Tool)}";
        Closing+=OnClosing;PreviewKeyDown+=OnKey;Loaded+=OnLoaded;
    }

    private void OnLoaded(object sender,RoutedEventArgs e){if(_startupInitialized)return;_startupInitialized=true;Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(InitializeAfterLoaded));}
    private void InitializeAfterLoaded(){try{LoadArgs();_help.Text=ToolHelp(_canvas.Tool);Refresh(true);}catch(Exception ex){_status.Text="编辑器初始化失败，仍可打开文件或关闭窗口。";Error(ex);}}

    private UIElement BuildUi()
    {
        var root=new DockPanel{Background=new SolidColorBrush(Color.FromRgb(47,51,58))};
        var top=new WrapPanel{Margin=new(8)};Add(top,"新建据点蓝图",NewBlueprintDocument);Add(top,"新建精修块",NewPatchDocument);Add(top,"打开",Open);Add(top,"保存",Save);Add(top,"另存为",SaveAs);Add(top,"撤销",Undo);Add(top,"重做",Redo);Add(top,"检查问题",Validate);Add(top,"放大",_canvas.ZoomIn);Add(top,"缩小",_canvas.ZoomOut);Add(top,"100%",_canvas.ActualSize);Add(top,"适应窗口",_canvas.Fit);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var bottom=new Border{Padding=new(8),Background=Brushes.Black,Child=_status};_status.Foreground=Brushes.White;DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new(180)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new(270)});root.Children.Add(grid);
        var left=new StackPanel{Margin=new(8)};Grid.SetColumn(left,0);grid.Children.Add(left);left.Children.Add(Header("工具"));foreach(var tool in Enum.GetValues<FineTool>()){var captured=tool;Add(left,ToolText(captured),()=>SelectTool(captured));}left.Children.Add(Header("当前工具说明"));left.Children.Add(_help);left.Children.Add(Label("基础地形"));left.Children.Add(_base);left.Children.Add(Label("地貌特征"));left.Children.Add(_feature);left.Children.Add(Label("特征密度"));left.Children.Add(_density);left.Children.Add(Label("笔刷大小（连续世界格）"));left.Children.Add(_brush);Add(left,"填充选区或连通区域",()=>{Sync();_canvas.FillSelectionOrRegion();});Add(left,"清除选区",_canvas.ClearSelection);Add(left,"添加对象",AddObject);
        Grid.SetColumn(_canvas,1);grid.Children.Add(_canvas);
        var right=new StackPanel{Margin=new(8)};Grid.SetColumn(right,2);grid.Children.Add(right);right.Children.Add(Header("文档 / 对象属性"));right.Children.Add(_info);foreach(var field in new[]{("对象类型 ID",_kind),("Content 引用",_contentRef),("Asset 引用",_assetRef),("X",_x),("Y",_y),("宽度",_w),("高度",_h)}){right.Children.Add(Label(field.Item1));right.Children.Add(field.Item2);}Add(right,"应用对象属性",ApplyObject);Add(right,"顺时针旋转 90°",_canvas.RotateSelected);Add(right,"复制",_canvas.DuplicateSelected);Add(right,"删除",_canvas.DeleteSelected);return root;
    }

    private void SelectTool(FineTool tool){Sync();if(tool==FineTool.BaseTerrainBrush)_canvas.ActiveBaseLayer=true;if(tool==FineTool.FeatureBrush)_canvas.ActiveBaseLayer=false;_canvas.Tool=tool;_help.Text=ToolHelp(tool);_status.Text="当前工具："+ToolText(tool);}
    private void Sync(){_canvas.BaseTerrain=Selected(_base,BaseTerrain.Plain);_canvas.Feature=Selected(_feature,TerrainFeature.Forest);_canvas.FeatureDensity=_density.Value;_canvas.BrushSize=(int)(_brush.SelectedItem??1);}
    private void Refresh(bool fit=false){Sync();_canvas.SetDocument(_document);var fields=Fields();_info.Foreground=Brushes.White;_info.Text=$"{DocumentType()}\n技术 ID：{fields.Id}\n名称：{fields.Name}\n尺寸：{fields.W}×{fields.H} 连续世界格\n未设置的格子：继承底层大世界";if(fit)Dispatcher.BeginInvoke(_canvas.Fit);UpdateTitle();}
    private void NewBlueprintDocument()=>Create(true);
    private void NewPatchDocument()=>Create(false);
    private void Create(bool blueprint){try{if(!CanDiscard())return;var values=Prompt(blueprint?"新建据点蓝图":"新建精修块",(blueprint?"blueprintId":"patchId",blueprint?"new_blueprint":"new_patch"),("名称",blueprint?"新据点蓝图":"新精修块"),("宽度（连续世界格）","37"),("高度（连续世界格）","23"));if(values==null)return;_document=blueprint?new WorldSiteBlueprintDocument{BlueprintId=values[0],DisplayName=values[1],WidthCells=Positive(values[2]),HeightCells=Positive(values[3])}:new DetailPatchDocument{PatchId=values[0],DisplayName=values[1],WidthCells=Positive(values[2]),HeightCells=Positive(values[3])};_path=null;Reset();Dirty();Refresh(true);}catch(Exception ex){Error(ex);}}
    private void Open(){if(!CanDiscard())return;var dialog=new OpenFileDialog{Filter="连续世界制作源|*.worldsiteblueprint.json;*.detailpatch.json"};if(dialog.ShowDialog()==true)OpenPath(dialog.FileName);}
    private void OpenPath(string path){try{_document=path.EndsWith(SurfaceAuthoringJson.WorldSiteBlueprintExtension,StringComparison.OrdinalIgnoreCase)?SurfaceAuthoringJson.LoadWorldSiteBlueprint(path):path.EndsWith(SurfaceAuthoringJson.DetailPatchExtension,StringComparison.OrdinalIgnoreCase)?SurfaceAuthoringJson.LoadDetailPatch(path):throw new InvalidDataException("不支持此制作源文件扩展名。");_path=path;_dirty=false;Reset();Refresh(true);}catch(Exception ex){Error(ex);}}
    private void Save(){if(_path==null){SaveAs();return;}SaveTo(_path);}
    private void SaveAs(){var fields=Fields();var blueprint=_document is WorldSiteBlueprintDocument;var dialog=new SaveFileDialog{Filter=blueprint?"据点蓝图|*.worldsiteblueprint.json":"精修块|*.detailpatch.json",FileName=fields.Id+(blueprint?SurfaceAuthoringJson.WorldSiteBlueprintExtension:SurfaceAuthoringJson.DetailPatchExtension)};if(dialog.ShowDialog()==true){_path=dialog.FileName;SaveTo(_path);}}
    private void SaveTo(string path){try{if(_document is WorldSiteBlueprintDocument b)SurfaceAuthoringJson.Save(path,b);else SurfaceAuthoringJson.Save(path,(DetailPatchDocument)_document);_dirty=false;Refresh();_status.Text="已保存："+path;}catch(Exception ex){Error(ex);}}
    private void Validate(){var issues=_document is WorldSiteBlueprintDocument b?SurfaceAuthoringValidation.Validate(b):SurfaceAuthoringValidation.Validate((DetailPatchDocument)_document);MessageBox.Show(issues.Count==0?"未发现问题。":string.Join("\n",issues.Select(x=>$"[{SeverityText(x.Severity)}] {x.Message}")),"检查问题");}
    private void AddObject(){try{if(_document is not WorldSiteBlueprintDocument){MessageBox.Show("只有据点蓝图可以放置对象。","FineEditor");return;}var values=Prompt("添加对象",("placementId","object_001"),("kindId","object"),("宽度（连续世界格）","1"),("高度（连续世界格）","1"));if(values==null)return;_canvas.PendingObject=new(){PlacementId=values[0],KindId=values[1],WidthCells=Positive(values[2]),HeightCells=Positive(values[3])};_canvas.Tool=FineTool.ObjectPlacement;_status.Text="请在连续世界格上单击放置对象。";}catch(Exception ex){Error(ex);}}
    private void LoadInspector(BlueprintObjectPlacement? item){foreach(var box in new[]{_kind,_contentRef,_assetRef,_x,_y,_w,_h})box.Text="";if(item==null)return;_kind.Text=item.KindId;_contentRef.Text=item.ContentRef??"";_assetRef.Text=item.AssetRef??"";_x.Text=item.LocalSurfaceX.ToString();_y.Text=item.LocalSurfaceY.ToString();_w.Text=item.WidthCells.ToString();_h.Text=item.HeightCells.ToString();}
    private void ApplyObject(){try{if(string.IsNullOrWhiteSpace(_kind.Text))throw new InvalidDataException("对象类型 ID 不能为空。");_canvas.ApplyObjectProperties(_kind.Text,_contentRef.Text,_assetRef.Text,Integer(_x.Text),Integer(_y.Text),Positive(_w.Text),Positive(_h.Text));}catch(Exception ex){Error(ex);}}
    private void Undo(){if(_undo.Count==0)return;_redo.Push(Serialize());Deserialize(_undo.Pop());Dirty();Refresh();}
    private void Redo(){if(_redo.Count==0)return;_undo.Push(Serialize());Deserialize(_redo.Pop());Dirty();Refresh();}
    private string Serialize()=>_document is WorldSiteBlueprintDocument b?SurfaceAuthoringJson.Serialize(b):SurfaceAuthoringJson.Serialize((DetailPatchDocument)_document);
    private void Deserialize(string json){_document=_document is WorldSiteBlueprintDocument?System.Text.Json.JsonSerializer.Deserialize<WorldSiteBlueprintDocument>(json,SurfaceAuthoringJson.Options)!:System.Text.Json.JsonSerializer.Deserialize<DetailPatchDocument>(json,SurfaceAuthoringJson.Options)!;}
    private void Reset(){_undo.Clear();_redo.Clear();}
    private void Dirty(){_dirty=true;UpdateTitle();}
    private void UpdateTitle()=>Title=$"FineEditor · 精细编辑器 · {Fields().Name}{(_dirty?" *":"")}";
    private (bool IsBlueprint,string Id,string Name,int W,int H) Raw()=>_document is WorldSiteBlueprintDocument b?(true,b.BlueprintId,b.DisplayName,b.WidthCells,b.HeightCells):(false,((DetailPatchDocument)_document).PatchId,((DetailPatchDocument)_document).DisplayName,((DetailPatchDocument)_document).WidthCells,((DetailPatchDocument)_document).HeightCells);
    private (string Id,string Name,int W,int H) Fields(){var r=Raw();return(r.Id,r.Name,r.W,r.H);}
    private string DocumentType()=>_document is WorldSiteBlueprintDocument?"据点蓝图":"精修块";
    private void LoadArgs(){var args=Environment.GetCommandLineArgs();if(args.Length>1&&File.Exists(args[1]))OpenPath(args[1]);}
    private void OnKey(object sender,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0){if(e.Key==Key.S){if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)SaveAs();else Save();e.Handled=true;}else if(e.Key==Key.Z){Undo();e.Handled=true;}else if(e.Key==Key.Y){Redo();e.Handled=true;}}else if(e.Key==Key.Delete){_canvas.DeleteSelected();e.Handled=true;}else if(e.Key==Key.Escape){_canvas.Cancel();e.Handled=true;}}
    private void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(!CanDiscard())e.Cancel=true;}
    private bool CanDiscard(){if(!_dirty)return true;var result=MessageBox.Show("当前修改尚未保存。是否保存？","FineEditor · 精细编辑器",MessageBoxButton.YesNoCancel,MessageBoxImage.Warning);if(result==MessageBoxResult.Cancel)return false;if(result==MessageBoxResult.Yes){Save();return !_dirty;}return true;}

    private static string ToolText(FineTool tool)=>tool switch{FineTool.Select=>"选择",FineTool.BaseTerrainBrush=>"基础地形笔刷",FineTool.FeatureBrush=>"地貌特征笔刷",FineTool.Eraser=>"擦除",FineTool.Fill=>"填充",FineTool.RectangleSelect=>"矩形选择",FineTool.ObjectPlacement=>"放置对象",_=>"未知工具"};
    private static string ToolHelp(FineTool tool)=>tool switch{FineTool.Select=>"选择并拖动对象。",FineTool.BaseTerrainBrush=>"绘制平地、山地或水域；水域会清除森林。",FineTool.FeatureBrush=>"绘制森林和密度；水域不能生成森林。",FineTool.Eraser=>"擦除当前格子的精修覆盖。",FineTool.Fill=>"填充选区或当前连通区域。",FineTool.RectangleSelect=>"拖动框选连续世界格。",FineTool.ObjectPlacement=>"单击放置准备好的对象。",_=>string.Empty};
    private static string SeverityText(ValidationSeverity value)=>value==ValidationSeverity.Error?"错误":"警告";
    private static T Selected<T>(ComboBox combo,T fallback)=>combo.SelectedValue is T value?value:fallback;
    private static ComboBox Combo<T>(IEnumerable<Choice<T>> values)=>new(){ItemsSource=values,DisplayMemberPath=nameof(Choice<T>.Text),SelectedValuePath=nameof(Choice<T>.Value)};
    private static int Positive(string value)=>int.TryParse(value,out var parsed)&&parsed>0?parsed:throw new InvalidDataException("请输入正整数。");
    private static int Integer(string value)=>int.TryParse(value,out var parsed)?parsed:throw new InvalidDataException("请输入整数坐标。");
    private static void Error(Exception ex)=>MessageBox.Show(ex is InvalidDataException?ex.Message:"操作失败。请检查文件路径、权限和文档内容。","FineEditor · 操作失败",MessageBoxButton.OK,MessageBoxImage.Error);
    private static TextBlock Header(string text)=>new(){Text=text,Foreground=Brushes.LightSkyBlue,FontWeight=FontWeights.Bold,Margin=new(0,8,0,5)};
    private static TextBlock Label(string text)=>new(){Text=text,Foreground=Brushes.White,Margin=new(0,5,0,2)};
    private static void Add(Panel panel,string text,Action action){var button=new Button{Content=text,Margin=new(0,0,5,5),Padding=new(7,3,7,3)};button.Click+=(_,_)=>action();panel.Children.Add(button);}
    private static string[]? Prompt(string title,params(string Label,string Value)[] fields){var window=new Window{Title=title,Width=390,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterScreen};var panel=new StackPanel{Margin=new(12)};var boxes=new List<TextBox>();foreach(var field in fields){panel.Children.Add(new TextBlock{Text=field.Label});var box=new TextBox{Text=field.Value,Margin=new(0,2,0,7)};boxes.Add(box);panel.Children.Add(box);}var ok=new Button{Content="确定",IsDefault=true,Width=70,HorizontalAlignment=HorizontalAlignment.Right};ok.Click+=(_,_)=>window.DialogResult=true;panel.Children.Add(ok);window.Content=panel;return window.ShowDialog()==true?boxes.Select(x=>x.Text.Trim()).ToArray():null;}
    private sealed record Choice<T>(T Value,string Text);
}
