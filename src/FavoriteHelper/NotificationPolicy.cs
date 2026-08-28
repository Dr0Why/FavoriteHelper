namespace FavoriteHelper
{
    internal static class NotificationPolicy
    {
        public static bool ShouldShow(bool safety, bool routineEnabled) { return safety || routineEnabled; }
        public static bool IsSafety(FavoriteResult result) { return result.State == FavoriteState.Broken || result.State == FavoriteState.Conflict; }
        public static bool ShouldShow(FavoriteResult result, bool routineEnabled) { return ShouldShow(IsSafety(result), routineEnabled); }
    }
}
