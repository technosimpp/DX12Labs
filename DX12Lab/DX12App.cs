namespace DX12Lab;

public class DX12App : AppBase
{
    private RenderingSystem _renderer;
    private Camera _camera = new();
    private DebugMenu _menu;
    private double _titleTimer;
    private int _frameCount;
    private bool _cWasDown, _fWasDown, _lWasDown, _kWasDown, _zWasDown;

    public DX12App() : base("DX12 Lab", 1280, 720) { }

    protected override void Init()
    {
        _renderer = new RenderingSystem(Window.Hwnd, Window.Width, Window.Height);

        _renderer.DisplacementScale = 0.02f;
        _renderer.TessMax = 16f;

        _menu = new DebugMenu(Window, _renderer);

        Window.OnResize += (width, height) => _renderer.Resize(width, height);
    }

    protected override void OnUpdate(double deltaTime)
    {
        _camera.Update(Input, (float)deltaTime);
        _renderer.CameraPos = _camera.Position;
        _renderer.CameraTarget = _camera.Target;

        bool cDown = Input.IsKeyDown(0x43);
        if (cDown && !_cWasDown)
        {
            _renderer.FrustumCullingEnabled = !_renderer.FrustumCullingEnabled;
            _menu.UpdateCheckbox("Frustum Culling", _renderer.FrustumCullingEnabled);
        }
        _cWasDown = cDown;

        bool fDown = Input.IsKeyDown(0x46);
        if (fDown && !_fWasDown)
        {
            _renderer.FreezeFrustum = !_renderer.FreezeFrustum;
            _menu.UpdateCheckbox("Freeze Frustum", _renderer.FreezeFrustum);
        }
        _fWasDown = fDown;

        bool lDown = Input.IsKeyDown(0x4C);
        if (lDown && !_lWasDown)
        {
            _renderer.ColorCodeLods = !_renderer.ColorCodeLods;
            _menu.UpdateCheckbox("Color-code LODs", _renderer.ColorCodeLods);
        }
        _lWasDown = lDown;

        bool kDown = Input.IsKeyDown(0x4B);
        if (kDown && !_kWasDown)
        {
            _renderer.SkirtsEnabled = !_renderer.SkirtsEnabled;
            _menu.UpdateCheckbox("Skirts (Seams)", _renderer.SkirtsEnabled);
        }
        _kWasDown = kDown;

        bool zDown = Input.IsKeyDown(0x5A);
        if (zDown && !_zWasDown)
        {
            _renderer.WireframeEnabled = !_renderer.WireframeEnabled;
            _menu.UpdateCheckbox("Wireframe", _renderer.WireframeEnabled);
        }
        _zWasDown = zDown;
        
        _frameCount++;
        _titleTimer += deltaTime;
        if (_titleTimer > 0.1)
        {
            double fps = _frameCount / _titleTimer;
            _menu.SetFps(fps);
            _menu.SetStats(
                _renderer.TerrainRenderedNodes,
                _renderer.TerrainTotalNodes,
                _renderer.TerrainCulledNodes,
                _renderer.TerrainRenderedTriangles,
                _renderer.TerrainLodCounts);

            string cullStatus = _renderer.FrustumCullingEnabled ? "ON" : "OFF";
            string freezeStatus = _renderer.FreezeFrustum ? " [FROZEN]" : "";

            Window.SetTitle(
                $"DX12 Lab 1: Terrain | Nodes: {_renderer.TerrainRenderedNodes}/{_renderer.TerrainTotalNodes} ({_renderer.TerrainCulledNodes} culled) | Tris: {_renderer.TerrainRenderedTriangles:N0} | Cull: {cullStatus}{freezeStatus} | FPS: {fps:0}");

            _titleTimer = 0;
            _frameCount = 0;
        }
    }

    protected override void OnRender()
    {
        _renderer.Render(Timer.DeltaTime);
    }

    protected override void Shutdown()
    {
        _renderer.Dispose();
    }
}