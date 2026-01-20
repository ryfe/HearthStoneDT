namespace HearthStoneDT.UI.Logs
{
    public static class StartingPointFinder
    {
        public static void Apply(LogFileTailer tailer, bool startFromEnd)
        {
            if (startFromEnd) tailer.StartFromEnd();
            else tailer.StartFromBeginning();
        }
    }
}
