using Supercent.PlayableAI.Common.Contracts;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Format
{
    public static class PlayableScenarioFacilityDefaults
    {
        public const int InputCountPerConversion = 1;
        public const float ConversionInterval = 0.15f;
        public const float InputItemMoveInterval = 0.1f;
        public const float SpawnInterval = 0.25f;
        public const int CustomerReqMin = 2;
        public const int CustomerReqMax = 3;
        public const int ItemStackMaxCount = 10;
        public const float ItemStackPopIntervalSeconds = 0f;
        public const int MoneyStackMaxCount = 0;
        public const float MoneyStackPopIntervalSeconds = 0f;
        public const float ConverterItemTweenDuration = 0.25f;
        public const float ConverterItemTweenParabolaHeight = 1.5f;
        public const float GeneratorItemTweenDuration = 0.25f;
        public const float GeneratorItemTweenParabolaHeight = 1f;
        public const bool GeneratorPlayPopAnimation = true;
        public const float SellerItemTweenDuration = 0.25f;
        public const float SellerItemTweenParabolaHeight = 3f;
        public const float SellerItemStackingSoundVolume = 1f;
        public const int SellerPitchInitialItemCountRef = 10;
        public static readonly Vector3 SellerMoneyRotationOffset = Vector3.zero;

        public static PlayableScenarioFacilityOptions CreateRoleOptions(string role)
        {
            var options = CreateBaseOptions();
            ApplyRoleDefaults(role, ref options);
            return options;
        }

        public static void ApplyRoleDefaults(string role, ref PlayableScenarioFacilityOptions options)
        {
            string normalizedRole = NormalizeRole(role);
            if (normalizedRole == PromptIntentObjectRoles.SELLER)
            {
                options.customerReqMin = CustomerReqMin;
                options.customerReqMax = CustomerReqMax;
                options.itemTweenDuration = SellerItemTweenDuration;
                options.itemTweenParabolaHeight = SellerItemTweenParabolaHeight;
                options.itemStackingSoundVolume = SellerItemStackingSoundVolume;
                options.pitchInitialItemCountRef = SellerPitchInitialItemCountRef;
                options.moneyRotationOffset = SellerMoneyRotationOffset;
                return;
            }

            if (normalizedRole == PromptIntentObjectRoles.PROCESSOR)
            {
                options.inputCountPerConversion = InputCountPerConversion;
                options.conversionInterval = ConversionInterval;
                options.inputItemMoveInterval = InputItemMoveInterval;
                options.itemTweenDuration = ConverterItemTweenDuration;
                options.itemTweenParabolaHeight = ConverterItemTweenParabolaHeight;
                options.itemStacker.maxCount = ItemStackMaxCount;
                return;
            }

            if (normalizedRole == PromptIntentObjectRoles.GENERATOR)
            {
                options.spawnInterval = SpawnInterval;
                options.itemTweenDuration = GeneratorItemTweenDuration;
                options.itemTweenParabolaHeight = GeneratorItemTweenParabolaHeight;
                options.playPopAnimation = GeneratorPlayPopAnimation;
                options.itemStacker.maxCount = ItemStackMaxCount;
            }
        }

        private static PlayableScenarioFacilityOptions CreateBaseOptions()
        {
            return new PlayableScenarioFacilityOptions
            {
                conversionInterval = ConversionInterval,
                inputCountPerConversion = InputCountPerConversion,
                inputItemMoveInterval = InputItemMoveInterval,
                itemTweenDuration = ConverterItemTweenDuration,
                itemTweenParabolaHeight = ConverterItemTweenParabolaHeight,
                playPopAnimation = GeneratorPlayPopAnimation,
                itemStackingSoundVolume = SellerItemStackingSoundVolume,
                pitchInitialItemCountRef = SellerPitchInitialItemCountRef,
                moneyRotationOffset = SellerMoneyRotationOffset,
                playerDropInterval = 0f,
                playerGetItemInterval = 0f,
                spawnInterval = SpawnInterval,
                customerSellingInterval = 0f,
                costPerMoneyPile = 0,
                customerReqMin = CustomerReqMin,
                customerReqMax = CustomerReqMax,
                itemStacker = new PlayableScenarioFacilityOptions.StackerTuning
                {
                    maxCount = ItemStackMaxCount,
                    popIntervalSeconds = ItemStackPopIntervalSeconds,
                },
                moneyStacker = new PlayableScenarioFacilityOptions.StackerTuning
                {
                    maxCount = MoneyStackMaxCount,
                    popIntervalSeconds = MoneyStackPopIntervalSeconds,
                },
            };
        }

        private static string NormalizeRole(string role)
        {
            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        }
    }
}
