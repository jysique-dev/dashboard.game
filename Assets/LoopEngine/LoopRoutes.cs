using UnityEngine;

public static class LoopRoutes
{
    private const string ToolRoute = "Tools/LoopEngine";
    
    //Windows
    public const string EditorToolRoute = ToolRoute + "/EditorTools";
    public const string BuilderToolRoute = EditorToolRoute + "/Builder";
    public const string ColorToolRoute = EditorToolRoute + "/Color Palette";
    public const string WFCToolRoute = EditorToolRoute + "/WFC/Socket Library";
    public const string WFCModuleGaleryRoute = EditorToolRoute + "/WFC/Module Gallery";
    public const string WFCNeighbotMatrix = EditorToolRoute + "/WFC/Neighbor Matrix";


    //Rutas 
    public const string FilesRoute = "LoopEngine";
    public const string CameraSettings = FilesRoute + "/Camera/Settings";
    public const string InputReader = FilesRoute + "/Input/Input Reader";
    public const string CameraBounds = FilesRoute + "/Camera/Camera Bounds";
    public const string PlaceableDefinition = FilesRoute + "/Grid/Placeable Definition";
    public const string CityLayout = FilesRoute + "/Grid/City Layout";
    public const string ColorPalette = FilesRoute + "/Color/Color Palette";


    public const string GridSettings = FilesRoute + "/Grid/Settings";
    public const string GridHost = FilesRoute + "/Grid/GridHost (int)";
    public const string GridInteractor = FilesRoute + "/Grid/GridInteractor";
    public const string GridhoverHighlighter = FilesRoute + "/Grid/Grid Hover Highlighter";
    public const string GridPointer = FilesRoute + "/Grid/Grid Pointer";

    public const string WFCSocketLibrary = FilesRoute + "/WFC/Socket Library";

    //File
    private const string Root = "Assets/LoopEngine";

    //"Assets/_Project/Samples/WfcTest";

}
