using XianXia.Core.Npc;

namespace XianXia.Unity.Host
{
    public static class HostMortalActivityPresentation
    {
        public static string Describe(MortalCivilianState state)
        {
            if (state == null) return "空闲";
            if (state.SleepPhase == CivilianSleepPhase.TravelingToResidence) return "前往住所";
            if (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence) return "睡眠";
            if (state.SleepPhase == CivilianSleepPhase.SleepingOnGround) return "就地休息";
            switch (state.FoodFetchPhase)
            {
                case FoodFetchPhase.NoFoodSource: return "饥饿·没有合法食物来源";
                case FoodFetchPhase.NoFoodAvailable: return "饥饿·公库无粮";
                case FoodFetchPhase.NoStorage: return "饥饿·无可用储藏室";
                case FoodFetchPhase.AccessDenied: return "饥饿·无权取粮";
                case FoodFetchPhase.PathUnavailable: return "取食受阻";
                case FoodFetchPhase.WaitingForPath:
                case FoodFetchPhase.Traveling:
                case FoodFetchPhase.Arrived: return "前往储藏室";
                case FoodFetchPhase.Eating: return "进食";
            }
            var activity = state.Activity;
            switch (activity)
            {
                case MortalActivity.FetchFood: return "需要进食";
                case MortalActivity.Eat: return "进食";
                case MortalActivity.SleepAtResidence: return "前往住所";
                case MortalActivity.SleepOnGround: return "就地休息";
                case MortalActivity.FarmerWork: return "种田";
                case MortalActivity.HerbFarmerWork: return "种药";
                case MortalActivity.Logging: return "伐木";
                case MortalActivity.MedicCare: return "照料伤员";
                case MortalActivity.Rescue: return "救援";
                case MortalActivity.Haul: return "搬运";
                case MortalActivity.Construction: return "施工";
                case MortalActivity.Flee: return "逃亡";
                case MortalActivity.Escorted: return "押送中";
                case MortalActivity.Detained: return "囚禁";
                default: return "空闲";
            }
        }

        public static string Profession(MortalProfession profession)
        {
            switch (profession)
            {
                case MortalProfession.Farmer: return "粮农";
                case MortalProfession.HerbFarmer: return "药农";
                case MortalProfession.Logger: return "樵夫";
                case MortalProfession.Medic: return "医者";
                default: return "未分配";
            }
        }
    }
}
