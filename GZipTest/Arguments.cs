namespace GZipTest
{
    public class Arguments
    {
        public Arguments()
        {
            TaskType = TaskType.Unknown;
        }

        public TaskType TaskType { get; set; }

        public string SourceFilePath { get; set; }

        public string DestinationFilePath { get; set; }
    }
}
