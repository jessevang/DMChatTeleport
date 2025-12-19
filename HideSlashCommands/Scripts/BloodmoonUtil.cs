namespace DMChatTeleport
{
    public static class BloodMoonUtil
    {
        private static bool TryGetTime(out int day, out int hour, out int bmDay, out int dusk, out int dawn)
        {
            day = hour = bmDay = dusk = dawn = 0;

            var world = GameManager.Instance?.World;
            if (world == null)
                return false;

            day = GameUtils.WorldTimeToDays(world.worldTime);
            hour = GameUtils.WorldTimeToHours(world.worldTime);

            bmDay = GameStats.GetInt(EnumGameStats.BloodMoonDay);
            var duskDawn = GameUtils.CalcDuskDawnHours(GameStats.GetInt(EnumGameStats.DayLightLength));
            dusk = duskDawn.duskHour;
            dawn = duskDawn.dawnHour;

            return true;
        }

        public static bool IsActiveNow()
        {
            if (!TryGetTime(out int day, out int hour, out int bmDay, out int dusk, out int dawn))
                return false;

            if (bmDay <= 0)
                return false;

            var duskDawn = GameUtils.CalcDuskDawnHours(GameStats.GetInt(EnumGameStats.DayLightLength));
            return GameUtils.IsBloodMoonTime(duskDawn, hour, bmDay, day);
        }

        public static (int day, int hour, int bmDay, int dusk, int dawn) GetDebugInfo()
        {
            if (!TryGetTime(out int day, out int hour, out int bmDay, out int dusk, out int dawn))
                return (0, 0, 0, 0, 0);

            return (day, hour, bmDay, dusk, dawn);
        }

        public static string GetDebugString()
        {
            var info = GetDebugInfo();
            return $"Day={info.day} Hour={info.hour} | BMDay={info.bmDay} | Window={info.dusk}:00->{info.dawn}:00";
        }
    }
}
