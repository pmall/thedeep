using Godot;

namespace TheDeep;

/// <summary>
/// A world's save on disk (<see cref="WorldDefinition.SavePath"/>: the prototype's is user://save.json; on Linux,
/// ~/.local/share/godot/app_userdata/The Deep/).
/// </summary>
public static class SaveFile
{
    public static SaveData? Read(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file is null ? null : SaveData.FromJson(file.GetAsText());
    }

    /// <summary>Forgets the save (the run ended): the next launch is a new game.</summary>
    public static void Delete(string path)
    {
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
    }

    public static void Write(string path, SaveData data)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"could not write {path}: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(data.ToJson());
    }
}
