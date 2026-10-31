using Godot;

namespace TheDeep;

/// <summary>The save on disk: user://save.json (on Linux, ~/.local/share/godot/app_userdata/The Deep/).</summary>
public static class SaveFile
{
    public const string Path = "user://save.json";

    public static SaveData? Read()
    {
        if (!FileAccess.FileExists(Path))
        {
            return null;
        }

        using FileAccess? file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        return file is null ? null : SaveData.FromJson(file.GetAsText());
    }

    /// <summary>Forgets the save (the run ended): the next launch is a new game.</summary>
    public static void Delete()
    {
        if (FileAccess.FileExists(Path))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        }
    }

    public static void Write(SaveData data)
    {
        using FileAccess? file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"could not write {Path}: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(data.ToJson());
    }
}
