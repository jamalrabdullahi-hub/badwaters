using Godot;

namespace BadWaters.Game;

/// <summary>
/// Development aid: run the game with "-- --screenshot=path.png [--select=JPN] [--year=2020]" to save
/// one frame and quit. Used for automated visual checks; has no effect in normal play.
/// </summary>
public static class DevScreenshot
{
    public static string? Arg(string name) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];

    public static async void MaybeCapture(Node node)
    {
        if (Arg("fps") != null)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            // warm up (loading, texture upload, shader compile), then time a fixed window of frames
            for (var i = 0; i < 180; i++) await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
            var t0 = Time.GetTicksUsec(); var prev = t0; ulong worst = 0;
            for (var i = 0; i < 300; i++)
            {
                await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksUsec(); worst = Math.Max(worst, now - prev); prev = now;
            }
            GD.Print($"AVG FPS {300e6 / (Time.GetTicksUsec() - t0):0.0} · worst frame {worst / 1000.0:0.0} ms");
            GD.Print($"FPS {Engine.GetFramesPerSecond()} · process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms · draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} · primitives {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} · objects {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} · video mem {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1e6:0} MB");
            if (Arg("screenshot") == null) { node.GetTree().Quit(); return; }
        }
        var path = Arg("screenshot");
        if (path == null) return;
        for (var i = 0; i < 6; i++) await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
        var img = node.GetViewport().GetTexture().GetImage();
        img.SavePng(path);
        GD.Print($"Screenshot saved to {path}");
        node.GetTree().Quit();
    }
}
