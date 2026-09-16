using Microsoft.Win32;
using SurfaceAuthoring.Core;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WorldComposer;

public partial class MainWindow:Window
{
    private static readonly Choice<BaseTerrain>[] BaseChoices=[new(BaseTerrain.Plain,"平原"),new(BaseTerrain.Mountain,"山地"),new(BaseTerrain.Water,"水域")];
    private static readonly Choice<TerrainFeature>[] FeatureChoices=[new(TerrainFeature.None,"无"),new(TerrainFeature.Forest,"森林")];
    private static readonly Choice<RoadClass>[] RoadChoices=[new(RoadClass.Trail,"小径"),new(RoadClass.SmallRoad,"小路"),new(RoadClass.Road,"道路"),new(RoadClass.MajorRoad,"大道")];
    private static readonly Choice<string>[] ObjectChoices=[new("treeS","小树"),new("treeM","中树"),new("treeL","大树"),new("bridge","桥"),new("rock","岩石")];
    private readonly WorldComposerCanvas _canvas=new();
    private readonly TextBlock _info=new(),_status=new(),_problems=new(){TextWrapping=TextWrapping.Wrap},_help=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray};
    private readonly ComboBox _base=Combo(BaseChoices),_feature=Combo(FeatureChoices),_road=Combo(RoadChoices),_brush=new(){ItemsSource=new[]{1,2,4,8,16}};
    private readonly Slider _density=new(){Minimum=0,Maximum=1,Value=.75,TickFrequency=.1,IsSnapToTickEnabled=true};
    private readonly TextBox _riverWidth=new(){Text="5"};
    private readonly ComboBox _worldObject=Combo(ObjectChoices);
    private readonly StackPanel _toolOptions=new(),_inspector=new();
    private readonly Stack<string> _undo=[],_redo=[];
    private WorldCompositionDocument _document=NewDocument();
    private LinkedSourceSet _linked=new();
    private string? _path,_pendingSnapshot;
    private bool _dirty;
    private bool _startupInitialized;

    public MainWindow()
    {
        InitializeComponent();Content=BuildUi();_base.SelectedValue=BaseTerrain.Plain;_feature.SelectedValue=TerrainFeature.Forest;_road.SelectedValue=RoadClass.SmallRoad;_worldObject.SelectedValue="treeM";_brush.SelectedItem=1;
        _base.SelectionChanged+=(_,_)=>SyncTools();_feature.SelectionChanged+=(_,_)=>SyncTools();_road.SelectionChanged+=(_,_)=>SyncTools();_worldObject.SelectionChanged+=(_,_)=>SyncTools();_brush.SelectionChanged+=(_,_)=>SyncTools();_density.ValueChanged+=(_,_)=>SyncTools();_riverWidth.TextChanged+=(_,_)=>SyncTools();
        _canvas.EditStarted+=()=>_pendingSnapshot=SurfaceAuthoringJson.Serialize(_document);
        _canvas.EditCompleted+=()=>{if(_pendingSnapshot!=null&&_pendingSnapshot!=SurfaceAuthoringJson.Serialize(_document)){_undo.Push(_pendingSnapshot);_redo.Clear();MarkDirty();}_pendingSnapshot=null;Refresh();};
        _canvas.SelectionChanged+=_=>RefreshInspector();
        _canvas.PreviewStatusChanged+=message=>_status.Text=message;
        _canvas.UserNotice+=message=>_status.Text=message;
        _canvas.CursorCellChanged+=(x,y)=>_status.Text=x<0?"不在连续世界范围内":$"连续世界格：({x}, {y}) · 大地图编辑格：({x/10}, {y/10}) · 当前工具：{ToolText(_canvas.Tool)}";
        Closing+=OnClosing;PreviewKeyDown+=OnKey;Loaded+=OnLoaded;
    }

    private void OnLoaded(object sender,RoutedEventArgs e){if(_startupInitialized)return;_startupInitialized=true;Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(InitializeAfterLoaded));}
    private void InitializeAfterLoaded(){try{LoadArgs();SelectTool(ComposerTool.Select);Refresh(true);}catch(Exception ex){_status.Text="编辑器初始化失败，仍可打开文件或关闭窗口。";Error(ex);}}

    private UIElement BuildUi()
    {
        var root=new DockPanel{Background=new SolidColorBrush(Color.FromRgb(47,51,58))};
        var top=new WrapPanel{Margin=new(8)};Add(top,"新建",New);Add(top,"打开",Open);Add(top,"保存",Save);Add(top,"另存为",SaveAs);Add(top,"调整世界尺寸…",ResizeWorld);Add(top,"撤销",Undo);Add(top,"重做",Redo);Add(top,"检查问题",Validate);Add(top,"烘焙连续世界",Bake);Add(top,"导入当前项目世界…",ImportCurrentWorld);Add(top,"导出运行时兼容候选包…",ExportCandidate);Add(top,"发布到当前项目（兼容模式）…",PublishCompatibility);Add(top,"恢复上一次运行时发布…",()=>RestoreCompatibility(false));Add(top,"恢复首次迁移前版本…",()=>RestoreCompatibility(true));Add(top,"刷新引用资源",RefreshLinks);Add(top,"在精细编辑器中打开",OpenInFineEditor);Add(top,"放大",_canvas.ZoomIn);Add(top,"缩小",_canvas.ZoomOut);Add(top,"100%",_canvas.ActualSize);Add(top,"适应窗口",_canvas.Fit);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var bottom=new Border{Padding=new(8),Child=_status,Background=Brushes.Black};_status.Foreground=Brushes.White;DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var columns=new Grid();columns.ColumnDefinitions.Add(new(){Width=new(190)});columns.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});columns.ColumnDefinitions.Add(new(){Width=new(290)});root.Children.Add(columns);
        var left=new StackPanel{Margin=new(8)};Grid.SetColumn(left,0);columns.Children.Add(left);left.Children.Add(Header("工具"));foreach(var t in Enum.GetValues<ComposerTool>()){var captured=t;Add(left,ToolText(captured),()=>SelectTool(captured));}left.Children.Add(Header("当前工具说明"));left.Children.Add(_help);left.Children.Add(Header("工具选项"));left.Children.Add(_toolOptions);
        Grid.SetColumn(_canvas,1);columns.Children.Add(_canvas);
        var right=new StackPanel{Margin=new(8)};Grid.SetColumn(right,2);columns.Children.Add(right);right.Children.Add(Header("图层 / 上下文属性"));var showGrid=new CheckBox{Content="显示大地图编辑网格",IsChecked=true,Foreground=Brushes.White};showGrid.Checked+=(_,_)=>{_canvas.ShowEditorGrid=true;_canvas.InvalidateVisual();};showGrid.Unchecked+=(_,_)=>{_canvas.ShowEditorGrid=false;_canvas.InvalidateVisual();};right.Children.Add(showGrid);var chunks=new CheckBox{Content="显示运行块（仅调试）",Foreground=Brushes.White};chunks.Checked+=(_,_)=>{_canvas.ShowRuntimeChunkOverlay=true;_canvas.InvalidateVisual();};chunks.Unchecked+=(_,_)=>{_canvas.ShowRuntimeChunkOverlay=false;_canvas.InvalidateVisual();};right.Children.Add(chunks);right.Children.Add(_info);right.Children.Add(_inspector);right.Children.Add(Header("问题"));right.Children.Add(new ScrollViewer{Content=_problems,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=230});return root;
    }

    private void SelectTool(ComposerTool tool){SyncTools();if(_canvas.Tool is ComposerTool.River or ComposerTool.Road&&_canvas.Tool!=tool)_canvas.CancelOperation();_canvas.ActiveBaseLayer=tool==ComposerTool.Terrain;_canvas.EraseMode=false;_canvas.Tool=tool;BuildToolOptions(tool);_help.Text=ToolHelp(tool);_status.Text="当前工具："+ToolText(tool);}
    private void SyncTools(){_canvas.BaseTerrain=Selected(_base,BaseTerrain.Plain);_canvas.Feature=Selected(_feature,TerrainFeature.Forest);_canvas.FeatureDensity=_density.Value;_canvas.BrushSize=(int)(_brush.SelectedItem??1);_canvas.RoadClass=Selected(_road,RoadClass.SmallRoad);_canvas.WorldObjectKindId=Selected(_worldObject,"treeM");if(double.TryParse(_riverWidth.Text,out var width)&&width>0)_canvas.RiverWidth=width;}
    private void BuildToolOptions(ComposerTool tool)
    {
        _toolOptions.Children.Clear();void Field(string name,UIElement value){_toolOptions.Children.Add(Label(name));_toolOptions.Children.Add(value);}void PaintActions(){Add(_toolOptions,"绘制模式",()=>{_canvas.EraseMode=false;_status.Text="已切换为绘制模式。";});Add(_toolOptions,"擦除当前图层",()=>{_canvas.EraseMode=true;_status.Text="已切换为擦除当前图层。";});Add(_toolOptions,"填充选区或连通区域",()=>{SyncTools();_canvas.FillSelectionOrRegion();});}
        if(tool==ComposerTool.Terrain){Field("基础地形",_base);Field("笔刷大小（大地图编辑格）",_brush);PaintActions();}
        else if(tool==ComposerTool.Forest){Field("特征密度",_density);Field("笔刷大小（大地图编辑格）",_brush);PaintActions();}
        else if(tool==ComposerTool.River){Field("河流宽度",_riverWidth);Add(_toolOptions,"完成路径（Enter）",_canvas.FinishPath);}
        else if(tool==ComposerTool.Road){Field("道路等级",_road);Add(_toolOptions,"完成路径（Enter）",_canvas.FinishPath);}
        else if(tool==ComposerTool.WorldObject)Field("世界物件",_worldObject);
        else if(tool==ComposerTool.Blueprint)Add(_toolOptions,"添加已有据点蓝图…",AddBlueprint);
        else if(tool==ComposerTool.DetailPatch)Add(_toolOptions,"从选区创建精修块",CreatePatch);
        else if(tool==ComposerTool.RectangleSelect)Add(_toolOptions,"清除选区",_canvas.ClearSelection);
    }
    private void Refresh(bool fit=false){SyncTools();_canvas.SetDocument(_document,_linked);if(fit)Dispatcher.BeginInvoke(_canvas.Fit);RefreshInspector();UpdateTitle();}
    private void RefreshInspector()
    {
        _info.Foreground=Brushes.White;
        _info.Text=$"\n连续世界：{_document.SurfaceWidthCells}×{_document.SurfaceHeightCells} 格\n大地图编辑网格：{_document.WorldEditorGridWidth}×{_document.WorldEditorGridHeight}\n1 个大地图编辑格 = 10×10 连续世界格\n世界种子：{_document.WorldSeed}\n世界物件：{_document.WorldObjectPlacements.Count}";
        _inspector.Children.Clear();
        if(_canvas.SelectedCrossing is { } crossing)
        {
            _inspector.Children.Add(Text($"当前选择：道路/河流交叉口\n河流：{crossing.RiverPathId}\n道路：{crossing.RoadPathId}\n世界坐标：{crossing.X:0.##}, {crossing.Y:0.##}\n当前状态：{CrossingText(crossing.Resolution)}"));
            Add(_inspector,"设为桥",()=>_canvas.ResolveSelectedCrossing(CrossingResolution.Bridge));
            Add(_inspector,"设为浅滩",()=>_canvas.ResolveSelectedCrossing(CrossingResolution.Ford));
            Add(_inspector,"设为未处理",()=>_canvas.ResolveSelectedCrossing(CrossingResolution.Unresolved));
            return;
        }
        switch(_canvas.SelectedItem)
        {
            case RiverPathSource river:
                _inspector.Children.Add(Text($"当前选择：河流\n路径 ID：{river.PathId}\n河流宽度：{river.WidthCells:0.##}\n控制点数量：{river.ControlPoints.Count}"));
                Add(_inspector,"应用河流宽度",ApplySelectedPath);
                Add(_inspector,"插入控制点",_canvas.InsertControlPoint);
                if(_canvas.CanRemoveSelectedControlPoint) Add(_inspector,"删除控制点",_canvas.RemoveSelectedControlPoint);
                if(_canvas.SelectedControlPoint!=null) Add(_inspector,"应用控制点坐标",ApplySelectedControlPoint);
                Add(_inspector,"删除河流",_canvas.DeleteSelected);
                break;
            case RoadPathSource road:
                _inspector.Children.Add(Text($"当前选择：道路\n路径 ID：{road.PathId}\n道路等级：{RoadText(road.RoadClass)}\n控制点数量：{road.ControlPoints.Count}"));
                Add(_inspector,"应用道路等级",ApplySelectedPath);
                Add(_inspector,"插入控制点",_canvas.InsertControlPoint);
                if(_canvas.CanRemoveSelectedControlPoint) Add(_inspector,"删除控制点",_canvas.RemoveSelectedControlPoint);
                if(_canvas.SelectedControlPoint!=null) Add(_inspector,"应用控制点坐标",ApplySelectedControlPoint);
                Add(_inspector,"删除道路",_canvas.DeleteSelected);
                break;
            case WorldObjectPlacement item:
                PlacementInspector($"世界物件\n放置 ID：{item.PlacementId}\n类型：{ObjectText(item.KindId)}\n坐标：{item.WorldSurfaceX}, {item.WorldSurfaceY}\n尺寸：{item.WidthCells}×{item.HeightCells}",true,false);
                break;
            case WorldSiteBlueprintPlacement item:
                PlacementInspector($"据点蓝图\n放置 ID：{item.PlacementId}\n蓝图：{item.BlueprintId}\n坐标：{item.SurfaceCellX}, {item.SurfaceCellY}",true,true);
                break;
            case DetailPatchPlacement item:
                PlacementInspector($"精修块\n放置 ID：{item.PlacementId}\n精修块：{item.PatchId}\n坐标：{item.SurfaceCellX}, {item.SurfaceCellY}",false,true);
                break;
            default:
                _inspector.Children.Add(Text("当前选择：无\n使用“选择”工具点击道路、河流、世界物件、据点蓝图或精修块进行编辑。"));
                break;
        }
    }
    private void PlacementInspector(string text,bool rotatable,bool canOpenFine)
    {
        _inspector.Children.Add(Text("当前选择："+text));
        Add(_inspector,"应用精确世界坐标…",ApplySelectedPosition);
        if(rotatable)Add(_inspector,"顺时针旋转 90°",_canvas.RotateSelected);
        if(canOpenFine)Add(_inspector,"在精细编辑器中打开",OpenInFineEditor);
        Add(_inspector,"删除",_canvas.DeleteSelected);
    }
    private void ApplySelectedPosition(){try{var current=_canvas.SelectedItem switch{WorldObjectPlacement o=>(o.WorldSurfaceX,o.WorldSurfaceY),WorldSiteBlueprintPlacement b=>(b.SurfaceCellX,b.SurfaceCellY),DetailPatchPlacement p=>(p.SurfaceCellX,p.SurfaceCellY),_=>(int.MinValue,int.MinValue)};if(current.Item1==int.MinValue){MessageBox.Show("请先选择世界物件、据点蓝图或精修块。","WorldComposer");return;}var values=Prompt("精确世界坐标",("连续世界格 X",current.Item1.ToString()),("连续世界格 Y",current.Item2.ToString()));if(values!=null)_canvas.ApplySelectedPlacement(Integer(values[0]),Integer(values[1]));}catch(Exception ex){Error(ex);}}
    private void ApplySelectedPath(){try{if(_canvas.SelectedItem is RiverPathSource river){var values=Prompt("河流属性",("河流宽度",river.WidthCells.ToString(System.Globalization.CultureInfo.InvariantCulture)));if(values==null)return;if(!double.TryParse(values[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var width)||width<=0)throw new InvalidDataException("河流宽度必须是正数。");_canvas.ApplySelectedPath(width,RoadClass.SmallRoad);}else if(_canvas.SelectedItem is RoadPathSource road){var values=Prompt("道路等级",("道路等级（Trail / SmallRoad / Road / MajorRoad）",road.RoadClass.ToString()));if(values==null)return;if(!Enum.TryParse<RoadClass>(values[0],true,out var value))throw new InvalidDataException("道路等级无效。可用：Trail、SmallRoad、Road、MajorRoad。");_canvas.ApplySelectedPath(0,value);}else MessageBox.Show("请先选择河流或道路。","WorldComposer");}catch(Exception ex){Error(ex);}}
    private void ApplySelectedControlPoint(){try{if(_canvas.SelectedControlPoint is not { } point){MessageBox.Show("请先选择一个控制点。","WorldComposer");return;}var values=Prompt("精确控制点坐标",("连续世界格 X",point.X.ToString(System.Globalization.CultureInfo.InvariantCulture)),("连续世界格 Y",point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)));if(values==null)return;if(!double.TryParse(values[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var x)||!double.TryParse(values[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var y))throw new InvalidDataException("控制点坐标必须是数字。");_canvas.ApplySelectedControlPoint(x,y);}catch(Exception ex){Error(ex);}}
    private void MarkDirty(){_dirty=true;UpdateTitle();}
    private void UpdateTitle()=>Title=$"WorldComposer · 大世界拼装编辑器 · {_document.DisplayName}{(_dirty?" *":"")}";
    private static WorldCompositionDocument NewDocument()=>new(){CompositionId="main_continent",DisplayName="主大陆"};
    private void New(){try{if(!CanDiscard())return;var v=Prompt("新建世界组合",("compositionId","main_continent"),("世界名称","主大陆"),("连续世界宽度","1900"),("连续世界高度","850"),("世界种子","1"));if(v==null)return;_document=new(){CompositionId=v[0],DisplayName=v[1],SurfaceWidthCells=Positive(v[2]),SurfaceHeightCells=Positive(v[3]),WorldSeed=Seed(v[4])};_path=null;_linked=new();ResetHistory();MarkDirty();Refresh(true);}catch(Exception ex){Error(ex);}}
    private void Open(){if(!CanDiscard())return;var dialog=new OpenFileDialog{Filter="世界组合|*.worldcomposition.json"};if(dialog.ShowDialog()!=true)return;try{_document=SurfaceAuthoringJson.LoadWorldComposition(dialog.FileName);_path=dialog.FileName;_dirty=false;ResetHistory();_linked=LinkedSourceLoader.Load(_document,_path);Refresh(true);Validate();}catch(Exception ex){Error(ex);}}
    private void ImportCurrentWorld()
    {
        if(!CanDiscard())return;var dialog=new OpenFolderDialog{Title="选择项目 Content 根目录",InitialDirectory=Path.Combine(Environment.CurrentDirectory,"Content")};if(dialog.ShowDialog()!=true)return;
        try{var result=LegacyWorldMigration.ImportCurrentWorld(dialog.FolderName);_document=result.Composition;_path=result.CompositionPath;_linked=LinkedSourceLoader.Load(_document,_path);_dirty=false;ResetHistory();Refresh(true);Validate();MessageBox.Show($"导入完成。\n世界：{_document.SurfaceWidthCells}×{_document.SurfaceHeightCells} 格\nSurface ID：{_document.RuntimeSurface?.SurfaceId}\n水域格：{result.WaterCellCount}\n道路路径：{result.RoadPathCount}\n桥：{result.BridgeCount}\n旧阻挡物：{result.LegacyBlockerCount}\n黄村蓝图：{result.HuangcunBlueprint.WidthCells}×{result.HuangcunBlueprint.HeightCells} 格\n黄村对象：{result.HuangcunObjectCount}\n\n未能自动迁移：{result.Warnings.Count} 项\n报告：{result.ReportPath}","当前项目世界导入");}catch(Exception ex){Error(ex);}
    }
    private void ExportCandidate()
    {
        if(_path==null){MessageBox.Show("导出前请先保存世界组合。","WorldComposer");return;}var contentRoot=FindContentRoot(_path);if(contentRoot==null){MessageBox.Show("无法从当前世界组合路径定位 Content 根目录。","WorldComposer");return;}var dialog=new OpenFolderDialog{Title="选择独立的候选输出目录"};if(dialog.ShowDialog()!=true)return;
        try{_linked=LinkedSourceLoader.Load(_document,_path);var files=LegacyWorldMigration.ExportCompatibilityCandidates(_document,_linked,contentRoot,dialog.FolderName);MessageBox.Show("兼容候选已写入独立目录，未修改运行时 Data。\n"+string.Join("\n",files),"导出兼容候选");}catch(Exception ex){Error(ex);}
    }
    private void PublishCompatibility()
    {
        try
        {
            if(_path==null||_dirty){MessageBox.Show("发布前请先保存当前 WorldComposition。","WorldComposer");return;}
            var contentRoot=FindContentRoot(_path);if(contentRoot==null){MessageBox.Show("无法从当前世界组合路径定位 Content 根目录。","WorldComposer");return;}
            _linked=LinkedSourceLoader.Load(_document,_path);var summary=CompatibilityPublisher.Prepare(_document,_linked,contentRoot);
            var message=$"当前 Composition：{summary.CompositionId}\nSurface 尺寸：{summary.WidthCells}×{summary.HeightCells}\nChunk 数量：{summary.ChunkCount}\nWater 格：{summary.WaterCells}\nRiver：{summary.RiverCount}\nRoad：{summary.RoadCount}\nBridge：{summary.BridgeCount}\nWorldObject：{summary.WorldObjectCount}\nHuangcun Blueprint 对象：{summary.HuangcunObjectCount}\n\n将替换：\n{summary.MainRuntimePath}\n{summary.GeographyRuntimePath}\n\n备份位置：\n{summary.BackupRoot}\n\n确认发布到当前项目旧 Runtime schema？";
            if(MessageBox.Show(message,"发布到当前项目（兼容模式）",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
            CompatibilityPublisher.Publish(_document,_linked,contentRoot);
            MessageBox.Show("兼容发布完成。当前 Unity Runtime schema 未改变。","WorldComposer");
        }catch(Exception ex){Error(ex);}
    }
    private void RestoreCompatibility(bool original)
    {
        try
        {
            if(_path==null||_document.LegacyMigration==null){MessageBox.Show("恢复要求已保存且带 LegacyMigration binding 的 WorldComposition。","WorldComposer");return;}
            var contentRoot=FindContentRoot(_path);if(contentRoot==null){MessageBox.Show("无法从当前世界组合路径定位 Content 根目录。","WorldComposer");return;}
            var label=original?"首次迁移前版本":"上一次运行时发布";
            if(MessageBox.Show($"将恢复{label}的两个 Runtime JSON。此操作会覆盖当前运行时文件，是否继续？","恢复运行时发布",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            if(MessageBox.Show("请再次确认：将整体恢复 main surface 与 geography 两个文件。","恢复运行时发布",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            CompatibilityPublisher.Restore(contentRoot,_document.LegacyMigration,original);
            MessageBox.Show($"已恢复{label}。","WorldComposer");
        }catch(Exception ex){Error(ex);}
    }
    private void ResizeWorld()
    {
        try
        {
            var request=ShowResizeDialog(_document.SurfaceWidthCells,_document.SurfaceHeightCells);
            if(request==null)return;
            var result=WorldCompositionResize.TryResize(_document,_linked,request.Value.Width,request.Value.Height,request.Value.Anchor);
            if(!result.Succeeded){MessageBox.Show("无法调整世界尺寸：\n"+string.Join("\n",result.BlockingReasons),"WorldComposer",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
            _undo.Push(SurfaceAuthoringJson.Serialize(_document));_redo.Clear();_document=result.Document!;MarkDirty();Refresh(true);
            _status.Text=$"世界已调整为 {_document.SurfaceWidthCells}×{_document.SurfaceHeightCells} 格。";
        }catch(Exception ex){Error(ex);}
    }
    private void Save(){if(_path==null){SaveAs();return;}SaveTo(_path);}
    private void SaveAs(){var dialog=new SaveFileDialog{Filter="世界组合|*.worldcomposition.json",FileName=_document.CompositionId+SurfaceAuthoringJson.WorldCompositionExtension};if(dialog.ShowDialog()==true){_path=dialog.FileName;SaveTo(_path);}}
    private void SaveTo(string path){try{SurfaceAuthoringJson.Save(path,_document);_dirty=false;UpdateTitle();_status.Text="已保存："+path;}catch(Exception ex){Error(ex);}}
    private void Undo(){if(_undo.Count==0)return;_redo.Push(SurfaceAuthoringJson.Serialize(_document));_document=System.Text.Json.JsonSerializer.Deserialize<WorldCompositionDocument>(_undo.Pop(),SurfaceAuthoringJson.Options)!;MarkDirty();Refresh();}
    private void Redo(){if(_redo.Count==0)return;_undo.Push(SurfaceAuthoringJson.Serialize(_document));_document=System.Text.Json.JsonSerializer.Deserialize<WorldCompositionDocument>(_redo.Pop(),SurfaceAuthoringJson.Options)!;MarkDirty();Refresh();}
    private void ResetHistory(){_undo.Clear();_redo.Clear();}
    private void RefreshLinks(){_linked=LinkedSourceLoader.Load(_document,_path);Refresh();Validate();}
    private void Validate(){var issues=SurfaceAuthoringValidation.Validate(_document,_linked);_problems.Text=issues.Count==0?"未发现问题。":string.Join("\n",issues.Select(x=>$"[{SeverityText(x.Severity)}] {x.Message}"));}
    private void Bake(){if(_path==null){MessageBox.Show("烘焙前请先保存世界组合。","WorldComposer");return;}_linked=LinkedSourceLoader.Load(_document,_path);Refresh();var issues=SurfaceAuthoringValidation.Validate(_document,_linked);if(issues.Any(x=>x.Severity==ValidationSeverity.Error)){Validate();MessageBox.Show("存在验证错误，无法烘焙。请查看问题面板。","WorldComposer");return;}var dialog=new SaveFileDialog{Filter="连续世界烘焙文件|*.continuoussurface.bake.json",FileName=_document.CompositionId+SurfaceAuthoringJson.BakeExtension};if(dialog.ShowDialog()!=true)return;try{var bake=ContinuousSurfaceBaker.Bake(_document,_linked);ContinuousSurfaceBaker.Save(dialog.FileName,bake);MessageBox.Show($"烘焙完成。\n连续世界：{bake.SurfaceWidthCells}×{bake.SurfaceHeightCells} 格\n警告：{issues.Count(x=>x.Severity==ValidationSeverity.Warning)} 项","WorldComposer");}catch(Exception ex){Error(ex);}}
    private void AddBlueprint(){if(_path==null){MessageBox.Show("链接据点蓝图前请先保存世界组合。","WorldComposer");return;}var dialog=new OpenFileDialog{Filter="据点蓝图|*.worldsiteblueprint.json"};if(dialog.ShowDialog()!=true)return;try{var blueprint=SurfaceAuthoringJson.LoadWorldSiteBlueprint(dialog.FileName);var pending=new WorldSiteBlueprintPlacement{PlacementId=Unique("blueprint",_document.WorldSiteBlueprintPlacements.Select(x=>x.PlacementId)),BlueprintId=blueprint.BlueprintId,SourcePath=Path.GetRelativePath(Path.GetDirectoryName(_path)!,dialog.FileName)};_canvas.PendingBlueprint=pending;_linked.Blueprints[pending.PlacementId]=blueprint;_canvas.Tool=ComposerTool.Blueprint;_status.Text="请在连续世界中单击放置据点蓝图。";}catch(Exception ex){Error(ex);}}
    private void CreatePatch(){try{if(_path==null||_canvas.Selection==null){MessageBox.Show("请先保存世界组合，并用矩形选择工具框选连续世界区域。","WorldComposer");return;}var r=_canvas.Selection.Value;var values=Prompt("新建精修块",("patchId","detail_patch"),("精修块名称","精修块"));if(values==null)return;var dialog=new SaveFileDialog{Filter="精修块|*.detailpatch.json",FileName=values[0]+SurfaceAuthoringJson.DetailPatchExtension};if(dialog.ShowDialog()!=true)return;var patch=new DetailPatchDocument{PatchId=values[0],DisplayName=values[1],WidthCells=(int)r.Width,HeightCells=(int)r.Height};SurfaceAuthoringJson.Save(dialog.FileName,patch);var snapshot=SurfaceAuthoringJson.Serialize(_document);_document.DetailPatchPlacements.Add(new(){PlacementId=Unique("patch",_document.DetailPatchPlacements.Select(x=>x.PlacementId)),PatchId=patch.PatchId,SourcePath=Path.GetRelativePath(Path.GetDirectoryName(_path)!,dialog.FileName),SurfaceCellX=(int)r.X,SurfaceCellY=(int)r.Y});_undo.Push(snapshot);_redo.Clear();MarkDirty();RefreshLinks();OpenFine(dialog.FileName);}catch(Exception ex){Error(ex);}}
    private void OpenInFineEditor(){string? source=_canvas.SelectedItem switch{WorldSiteBlueprintPlacement b=>b.SourcePath,DetailPatchPlacement p=>p.SourcePath,_=>null};if(source==null||_path==null){MessageBox.Show("请先选择一个据点蓝图或精修块。","WorldComposer");return;}OpenFine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_path)!,source)));}
    private static void OpenFine(string path){var exe=Path.Combine(AppContext.BaseDirectory,"FineEditor.exe");if(!File.Exists(exe)){MessageBox.Show("未在 WorldComposer.exe 同目录找到 FineEditor.exe。请运行“编译-所有编辑器.cmd”，并从 Apps 目录启动。","WorldComposer");return;}Process.Start(new ProcessStartInfo(exe,$"\"{path}\""){UseShellExecute=true});}
    private void OnKey(object sender,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0){if(e.Key==Key.S){if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)SaveAs();else Save();e.Handled=true;}else if(e.Key==Key.Z){Undo();e.Handled=true;}else if(e.Key==Key.Y){Redo();e.Handled=true;}}else if(e.Key==Key.Delete){_canvas.DeleteSelected();e.Handled=true;}else if(e.Key==Key.Escape){_canvas.CancelOperation();e.Handled=true;}else if(e.Key==Key.Enter){_canvas.FinishPath();e.Handled=true;}}
    private void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(!CanDiscard())e.Cancel=true;}
    private bool CanDiscard(){if(!_dirty)return true;var result=MessageBox.Show("当前修改尚未保存。是否保存？","WorldComposer · 大世界拼装编辑器",MessageBoxButton.YesNoCancel,MessageBoxImage.Warning);if(result==MessageBoxResult.Cancel)return false;if(result==MessageBoxResult.Yes){Save();return !_dirty;}return true;}
    private void LoadArgs(){var args=Environment.GetCommandLineArgs();if(args.Length>1&&File.Exists(args[1]))try{_document=SurfaceAuthoringJson.LoadWorldComposition(args[1]);_path=args[1];_linked=LinkedSourceLoader.Load(_document,_path);}catch(Exception ex){Error(ex);}}

    private static string ToolText(ComposerTool tool)=>tool switch{ComposerTool.Select=>"选择",ComposerTool.Terrain=>"地形",ComposerTool.Forest=>"森林",ComposerTool.River=>"河流",ComposerTool.Road=>"道路",ComposerTool.WorldObject=>"世界物件",ComposerTool.Blueprint=>"据点蓝图",ComposerTool.DetailPatch=>"精修块",ComposerTool.RectangleSelect=>"矩形选择",_=>"未知工具"};
    private static string ToolHelp(ComposerTool tool)=>tool switch{ComposerTool.Select=>"单击道路、河流、蓝图或世界物件进行编辑；拖动控制点或放置项移动。",ComposerTool.Terrain=>"按住左键拖动绘制。Alt + 滚轮缩放，空格+拖动或中键平移。Ctrl+Z 撤销。",ComposerTool.Forest=>"左键绘制森林范围；森林可以通行。调整密度控制树林茂盛程度。",ComposerTool.River=>"单击增加河道控制点，双击或 Enter 完成；选择河流后可拖动控制点。",ComposerTool.Road=>"单击增加路线控制点，双击或 Enter 完成。",ComposerTool.WorldObject=>"选择物件类型后，在连续世界格上单击放置。",ComposerTool.Blueprint=>"选择已有据点蓝图，然后在连续世界中放置。",ComposerTool.DetailPatch=>"先用矩形选择范围，再创建并链接精修块。",ComposerTool.RectangleSelect=>"拖动框选范围，可供填充或创建精修块使用。",_=>string.Empty};
    private static string SelectedType(object? item)=>item switch{WorldObjectPlacement=>"世界物件",WorldSiteBlueprintPlacement=>"据点蓝图",DetailPatchPlacement=>"精修块",RiverPathSource=>"河流",RoadPathSource=>"道路",null=>"无",_=>"未知项"};
    private static string ObjectText(string kind)=>ObjectChoices.FirstOrDefault(x=>x.Value==kind)?.Text??kind;
    private static string RoadText(RoadClass value)=>RoadChoices.First(x=>x.Value.Equals(value)).Text;
    private static string CrossingText(CrossingResolution value)=>value switch{CrossingResolution.Bridge=>"桥",CrossingResolution.Ford=>"浅滩",_=>"未处理"};
    private static string SeverityText(ValidationSeverity value)=>value==ValidationSeverity.Error?"错误":"警告";
    private static T Selected<T>(ComboBox combo,T fallback)=>combo.SelectedValue is T value?value:fallback;
    private static ComboBox Combo<T>(IEnumerable<Choice<T>> values)=>new(){ItemsSource=values,DisplayMemberPath=nameof(Choice<T>.Text),SelectedValuePath=nameof(Choice<T>.Value)};
    private static string Unique(string prefix,IEnumerable<string> ids){var set=ids.ToHashSet(StringComparer.Ordinal);for(var i=1;;i++){var id=$"{prefix}_{i:000}";if(!set.Contains(id))return id;}}
    private static string? FindContentRoot(string path){for(var d=Directory.GetParent(Path.GetFullPath(path));d!=null;d=d.Parent)if(string.Equals(d.Name,"Content",StringComparison.OrdinalIgnoreCase))return d.FullName;return null;}
    private static int Positive(string value)=>int.TryParse(value,out var parsed)&&parsed>0?parsed:throw new InvalidDataException("请输入正整数。");
    private static int Integer(string value)=>int.TryParse(value,out var parsed)?parsed:throw new InvalidDataException("请输入整数坐标。");
    private static ulong Seed(string value)=>ulong.TryParse(value,out var parsed)?parsed:throw new InvalidDataException("世界种子必须是非负整数。");
    private static void Error(Exception ex)=>MessageBox.Show(ex is InvalidDataException?ex.Message:"操作失败。请检查文件路径、权限和文档内容。","WorldComposer · 操作失败",MessageBoxButton.OK,MessageBoxImage.Error);
    private static TextBlock Header(string text)=>new(){Text=text,Foreground=Brushes.LightSkyBlue,FontWeight=FontWeights.Bold,Margin=new(0,8,0,5)};
    private static TextBlock Text(string text)=>new(){Text=text,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,6)};
    private static TextBlock Label(string text)=>new(){Text=text,Foreground=Brushes.White,Margin=new(0,6,0,2)};
    private static void Add(Panel panel,string text,Action action){var button=new Button{Content=text,Margin=new(0,0,5,5),Padding=new(7,3,7,3)};button.Click+=(_,_)=>action();panel.Children.Add(button);}
    private static string[]? Prompt(string title,params(string Label,string Value)[] fields){var window=new Window{Title=title,Width=400,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterScreen};var panel=new StackPanel{Margin=new(12)};var boxes=new List<TextBox>();foreach(var field in fields){panel.Children.Add(new TextBlock{Text=field.Label});var box=new TextBox{Text=field.Value,Margin=new(0,2,0,7)};boxes.Add(box);panel.Children.Add(box);}var ok=new Button{Content="确定",IsDefault=true,Width=70,HorizontalAlignment=HorizontalAlignment.Right};ok.Click+=(_,_)=>window.DialogResult=true;panel.Children.Add(ok);window.Content=panel;return window.ShowDialog()==true?boxes.Select(x=>x.Text.Trim()).ToArray():null;}
    private static (int Width,int Height,WorldResizeAnchor Anchor)? ShowResizeDialog(int currentWidth,int currentHeight)
    {
        var window=new Window{Title="调整世界尺寸",Width=360,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new(14)};panel.Children.Add(new TextBlock{Text=$"当前：{currentWidth} × {currentHeight}",FontWeight=FontWeights.Bold});
        panel.Children.Add(new TextBlock{Text="新宽度（10 格整数倍）"});var width=new TextBox{Text=currentWidth.ToString(),Margin=new(0,2,0,7)};panel.Children.Add(width);
        panel.Children.Add(new TextBlock{Text="新高度（10 格整数倍）"});var height=new TextBox{Text=currentHeight.ToString(),Margin=new(0,2,0,7)};panel.Children.Add(height);
        panel.Children.Add(new TextBlock{Text="锚点",Margin=new(0,4,0,2)});
        var anchors=new Grid();anchors.ColumnDefinitions.Add(new ColumnDefinition());anchors.ColumnDefinitions.Add(new ColumnDefinition());var leftTop=new RadioButton{Content="左上固定",GroupName="anchor"};var rightTop=new RadioButton{Content="右上固定",GroupName="anchor"};var leftBottom=new RadioButton{Content="左下固定",GroupName="anchor",IsChecked=true};var rightBottom=new RadioButton{Content="右下固定",GroupName="anchor"};Grid.SetRow(leftTop,0);Grid.SetColumn(leftTop,0);Grid.SetColumn(rightTop,1);Grid.SetRow(leftBottom,1);Grid.SetColumn(leftBottom,0);Grid.SetRow(rightBottom,1);Grid.SetColumn(rightBottom,1);anchors.RowDefinitions.Add(new RowDefinition());anchors.RowDefinitions.Add(new RowDefinition());anchors.Children.Add(leftTop);anchors.Children.Add(rightTop);anchors.Children.Add(leftBottom);anchors.Children.Add(rightBottom);panel.Children.Add(anchors);
        var ok=new Button{Content="调整",IsDefault=true,Width=80,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,12,0,0)};ok.Click+=(_,_)=>window.DialogResult=true;panel.Children.Add(ok);window.Content=panel;
        if(window.ShowDialog()!=true)return null;
        if(!int.TryParse(width.Text,out var w)||!int.TryParse(height.Text,out var h))throw new InvalidDataException("世界尺寸必须是整数。");
        var anchor=leftBottom.IsChecked==true?WorldResizeAnchor.LeftBottom:rightBottom.IsChecked==true?WorldResizeAnchor.RightBottom:leftTop.IsChecked==true?WorldResizeAnchor.LeftTop:WorldResizeAnchor.RightTop;
        return(w,h,anchor);
    }
    private sealed record Choice<T>(T Value,string Text);
}
