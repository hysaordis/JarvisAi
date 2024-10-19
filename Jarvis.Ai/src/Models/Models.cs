namespace Jarvis.Ai.Models;
public class MemoryKeyResponse
{
    public string Key { get; set; }
}

public class WebUrl
{
    public string Url { get; set; }
}

public class CreateFileResponse
{
    public string FileContent { get; set; }

    public string FileName { get; set; }
}

public class FileSelectionResponse
{
    public string File { get; set; }

}

public class FileUpdateResponse
{
    public string Updates { get; set; }
}

public class FileDeleteResponse
{
    public string File { get; set; }

    public bool ForceDelete { get; set; }
}

public class FileReadResponse
{
    public string File { get; set; }

}

public class IsRunnable
{
    public bool CodeIsRunnable { get; set; }
}

public class MakeCodeRunnableResponse
{
    public List<string> ChangesDescribed { get; set; }

    public string FullUpdatedCode { get; set; }
}
