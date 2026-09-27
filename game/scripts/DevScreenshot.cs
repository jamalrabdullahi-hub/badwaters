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
        var path = Arg("screenshot");
        if (path == null) return;
        for (var i = 0; i < 6; i++) await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
        var img = node.GetViewport().GetTexture().GetImage();
        img.SavePng(path);
        GD.Print($"Screenshot saved to {path}");
        node.GetTree().Quit();
    }
}
