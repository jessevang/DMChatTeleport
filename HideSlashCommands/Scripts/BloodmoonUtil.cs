namespace DMChatTeleports
{
    public static class BloodMoonUtil
    {
        public static bool IsActiveNow()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
                return false;

            int day = GameUtils.WorldTimeToDays(world.worldTime);
            int hour = GameUtils.WorldTimeToHours(world.worldTime);

            int bmDay = GameStats.GetInt(EnumGameStats.BloodMoonDay);
            if (bmDay <= 0)
                return false;

            var duskDawn = GameUtils.CalcDuskDawnHours(GameStats.GetInt(EnumGameStats.DayLightLength));
            return GameUtils.IsBloodMoonTime(duskDawn, hour, bmDay, day);
        }


        public static (int day, int hour, int bmDay, int dusk, int dawn) GetDebugInfo()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
                return (0, 0, 0, 0, 0);

            int day = GameUtils.WorldTimeToDays(world.worldTime);
            int hour = GameUtils.WorldTimeToHours(world.worldTime);
            int bmDay = GameStats.GetInt(EnumGameStats.BloodMoonDay);

            var duskDawn = GameUtils.CalcDuskDawnHours(GameStats.GetInt(EnumGameStats.DayLightLength));
            return (day, hour, bmDay, duskDawn.duskHour, duskDawn.dawnHour);
        }
    }

}