using Godot;

namespace TheDeep;

/// <summary>
/// A world's save on disk (<see cref="WorldDefinition.SavePath"/>: the prototype's is user://save.json; on Linux,
/// ~/.local/share/godot/app_userdata/The Deep/).
/// </summary>
public static class SaveFile
{
    /// <summary>The save at <paramref name="path"/>, or null when there is none (a new game) or it cannot be opened.</summary>
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

    /// <summary>Writes <paramref name="data"/> as the save at <paramref name="path"/>, replacing the one there: written to a
    /// temporary file beside it first, then renamed over, so a crash mid-write never breaks the save; a failure is reported
    /// as an engine error.</summary>
    public static void Write(string path, SaveData data)
    {
        string temp = SaveData.TempPath(path);
        using (FileAccess? file = FileAccess.Open(temp, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushError($"could not write {temp}: {FileAccess.GetOpenError()}");
                return;
            }

            file.StoreString(data.ToJson());
        }

        Error error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(temp), ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok)
        {
            GD.PushError($"could not move {temp} over {path}: {error}");
        }
    }
}
