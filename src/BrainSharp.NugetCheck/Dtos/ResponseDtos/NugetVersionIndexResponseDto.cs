namespace BrainSharp.NugetCheck.Dtos.ResponseDtos;

public class NugetVersionIndexResponseDto
{
    public class Rootobject
    {
        public string[] type { get; set; }
        public string catalogEntry { get; set; }
        public bool listed { get; set; }
        public string packageContent { get; set; }
        public DateTime published { get; set; }
        public string registration { get; set; }
        public Context context { get; set; }
    }

    public class Context
    {
        public string vocab { get; set; }
        public string xsd { get; set; }
        public Catalogentry catalogEntry { get; set; }
        public Registration registration { get; set; }
        public Packagecontent packageContent { get; set; }
        public Published published { get; set; }
    }

    public class Catalogentry
    {
        public string type { get; set; }
    }

    public class Registration
    {
        public string type { get; set; }
    }

    public class Packagecontent
    {
        public string type { get; set; }
    }

    public class Published
    {
        public string type { get; set; }
    }

}