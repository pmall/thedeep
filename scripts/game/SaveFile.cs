using Godot;

namespace TheDeep;

/// <summary>
/// A world's save on disk (<see cref="WorldDefinition.SavePath"/>: the prototype's is user://save.json; on Linux,
/// ~/.local/share/godot/app_userdata/The Deep/).
/// </summary>
public static class SaveFile
{
    /// <summary>
    /// The save at <paramref name="path"/>, or null when there is none (a new game) or it cannot be opened. A save that cannot be
    /// read (corrupt, or from a newer version) is never overwritten: it is reported as an engine error naming the file and why, and
    /// set aside by <see cref="SetAside"/>, so the new game that follows keeps it.
    /// </summary>
    public static SaveData? Read(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            return null;
        }

        SaveReading reading = SaveData.Parse(file.GetAsText());
        if (reading is SaveReading.Loaded loaded)
        {
            return loaded.Data;
        }

        file.Close();
        string? kept = SetAside(path);
        GD.PushError($"the save {ProjectSettings.GlobalizePath(path)} was not read: {reading.Problem}; " +
                     (kept is null ? "it could not be set aside either" : $"it is kept as {ProjectSettings.GlobalizePath(kept)}") + ", and a new game starts");
        return null;
    }

    /// <summary>Renames the save at <paramref name="path"/> to <see cref="SaveData.AsidePath"/> (replacing an older one); the new path, or null on failure.</summary>
    public static string? SetAside(string path)
    {
        string aside = SaveData.AsidePath(path);
        return DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(path), ProjectSettings.GlobalizePath(aside)) == Error.Ok ? aside : null;
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
