namespace Supercent.PlayableAI.Common.Format
{
    public static class PlayableFeatureTypeIds
    {
        public const string Generator = "generator";
        public const string Converter = "converter";
        public const string Seller = "seller";
        public const string Rail = "rail";
        public const string PhysicsArea = "physics_area";

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [System.Serializable]
    public sealed class PlayableScenarioFeatureOptionDefinition
    {
        public string featureId;
        public string featureType;
        public string targetId;
        public PlayableScenarioFeatureOptions options;
    }

    [System.Serializable]
    public struct PlayableScenarioPlayerOptions
    {
        public PlayableScenarioFeatureOptions.StackerTuning itemStacker;
    }

    [System.Serializable]
    public struct GeneratorFeatureOptions
    {
        public float spawnInterval;
        public float itemTweenDuration;
        public float itemTweenParabolaHeight;
        public bool playPopAnimation;
        public float playerGetItemInterval;
        public PlayableScenarioFeatureOptions.StackerTuning itemStacker;
    }

    [System.Serializable]
    public struct ConverterFeatureOptions
    {
        public float conversionInterval;
        public int inputCountPerConversion;
        public float inputItemMoveInterval;
        public float itemTweenDuration;
        public float itemTweenParabolaHeight;
        public PlayableScenarioFeatureOptions.StackerTuning itemStacker;
    }

    [System.Serializable]
    public struct SellerFeatureOptions
    {
        public float itemTweenDuration;
        public float itemTweenParabolaHeight;
        public float itemStackingSoundVolume;
        public int pitchInitialItemCountRef;
        public UnityEngine.Vector3 moneyRotationOffset;
        public float playerDropInterval;
        public float customerSellingInterval;
        public int costPerMoneyPile;
        public int customerReqMin;
        public int customerReqMax;
        public PlayableScenarioFeatureOptions.StackerTuning itemStacker;
        public PlayableScenarioFeatureOptions.StackerTuning moneyStacker;
    }

    [System.Serializable]
    public struct RailFeatureOptions
    {
        public float spawnIntervalSeconds;
        public float travelDurationSeconds;
    }

    [System.Serializable]
    public struct PhysicsAreaFeatureOptions
    {
        public int itemsPerBlock;
    }

    [System.Serializable]
    public struct PlayableScenarioFeatureOptions
    {
        [System.Serializable]
        public struct StackerTuning
        {
            public int maxCount;
            public float popIntervalSeconds;
        }

        // Transitional flat surface retained so existing editor/runtime callers keep compiling
        // while the pipeline moves to featureType-specific payloads.
        public float conversionInterval;
        public int inputCountPerConversion;
        public float inputItemMoveInterval;
        public float itemTweenDuration;
        public float itemTweenParabolaHeight;
        public bool playPopAnimation;
        public float itemStackingSoundVolume;
        public int pitchInitialItemCountRef;
        public UnityEngine.Vector3 moneyRotationOffset;
        public float playerDropInterval;
        public float playerGetItemInterval;
        public float spawnInterval;
        public float customerSellingInterval;
        public int costPerMoneyPile;
        public int customerReqMin;
        public int customerReqMax;
        public StackerTuning itemStacker;
        public StackerTuning moneyStacker;

        public GeneratorFeatureOptions generator;
        public ConverterFeatureOptions converter;
        public SellerFeatureOptions seller;
        public RailFeatureOptions rail;
        public PhysicsAreaFeatureOptions physicsArea;

        public PlayableScenarioFeatureOptions NormalizeForFeatureType(string featureType)
        {
            PlayableScenarioFeatureOptions normalized = this;
            string normalizedType = PlayableFeatureTypeIds.Normalize(featureType);
            switch (normalizedType)
            {
                case PlayableFeatureTypeIds.Generator:
                    normalized.generator = new GeneratorFeatureOptions
                    {
                        spawnInterval = spawnInterval,
                        itemTweenDuration = itemTweenDuration,
                        itemTweenParabolaHeight = itemTweenParabolaHeight,
                        playPopAnimation = playPopAnimation,
                        playerGetItemInterval = playerGetItemInterval,
                        itemStacker = itemStacker,
                    };
                    break;
                case PlayableFeatureTypeIds.Converter:
                    normalized.converter = new ConverterFeatureOptions
                    {
                        conversionInterval = conversionInterval,
                        inputCountPerConversion = inputCountPerConversion,
                        inputItemMoveInterval = inputItemMoveInterval,
                        itemTweenDuration = itemTweenDuration,
                        itemTweenParabolaHeight = itemTweenParabolaHeight,
                        itemStacker = itemStacker,
                    };
                    break;
                case PlayableFeatureTypeIds.Seller:
                    normalized.seller = new SellerFeatureOptions
                    {
                        itemTweenDuration = itemTweenDuration,
                        itemTweenParabolaHeight = itemTweenParabolaHeight,
                        itemStackingSoundVolume = itemStackingSoundVolume,
                        pitchInitialItemCountRef = pitchInitialItemCountRef,
                        moneyRotationOffset = moneyRotationOffset,
                        playerDropInterval = playerDropInterval,
                        customerSellingInterval = customerSellingInterval,
                        costPerMoneyPile = costPerMoneyPile,
                        customerReqMin = customerReqMin,
                        customerReqMax = customerReqMax,
                        itemStacker = itemStacker,
                        moneyStacker = moneyStacker,
                    };
                    break;
                case PlayableFeatureTypeIds.Rail:
                    normalized.rail = new RailFeatureOptions
                    {
                        spawnIntervalSeconds = rail.spawnIntervalSeconds > 0f ? rail.spawnIntervalSeconds : spawnInterval,
                        travelDurationSeconds = rail.travelDurationSeconds,
                    };
                    break;
                case PlayableFeatureTypeIds.PhysicsArea:
                    normalized.physicsArea = new PhysicsAreaFeatureOptions
                    {
                        itemsPerBlock = physicsArea.itemsPerBlock,
                    };
                    break;
            }

            return normalized;
        }

        public static PlayableScenarioFeatureOptions FromGenerator(GeneratorFeatureOptions generatorOptions)
        {
            return new PlayableScenarioFeatureOptions
            {
                generator = generatorOptions,
                spawnInterval = generatorOptions.spawnInterval,
                itemTweenDuration = generatorOptions.itemTweenDuration,
                itemTweenParabolaHeight = generatorOptions.itemTweenParabolaHeight,
                playPopAnimation = generatorOptions.playPopAnimation,
                playerGetItemInterval = generatorOptions.playerGetItemInterval,
                itemStacker = generatorOptions.itemStacker,
            };
        }

        public static PlayableScenarioFeatureOptions FromConverter(ConverterFeatureOptions converterOptions)
        {
            return new PlayableScenarioFeatureOptions
            {
                converter = converterOptions,
                conversionInterval = converterOptions.conversionInterval,
                inputCountPerConversion = converterOptions.inputCountPerConversion,
                inputItemMoveInterval = converterOptions.inputItemMoveInterval,
                itemTweenDuration = converterOptions.itemTweenDuration,
                itemTweenParabolaHeight = converterOptions.itemTweenParabolaHeight,
                itemStacker = converterOptions.itemStacker,
            };
        }

        public static PlayableScenarioFeatureOptions FromSeller(SellerFeatureOptions sellerOptions)
        {
            return new PlayableScenarioFeatureOptions
            {
                seller = sellerOptions,
                itemTweenDuration = sellerOptions.itemTweenDuration,
                itemTweenParabolaHeight = sellerOptions.itemTweenParabolaHeight,
                itemStackingSoundVolume = sellerOptions.itemStackingSoundVolume,
                pitchInitialItemCountRef = sellerOptions.pitchInitialItemCountRef,
                moneyRotationOffset = sellerOptions.moneyRotationOffset,
                playerDropInterval = sellerOptions.playerDropInterval,
                customerSellingInterval = sellerOptions.customerSellingInterval,
                costPerMoneyPile = sellerOptions.costPerMoneyPile,
                customerReqMin = sellerOptions.customerReqMin,
                customerReqMax = sellerOptions.customerReqMax,
                itemStacker = sellerOptions.itemStacker,
                moneyStacker = sellerOptions.moneyStacker,
            };
        }

        public static PlayableScenarioFeatureOptions FromRail(RailFeatureOptions railOptions)
        {
            return new PlayableScenarioFeatureOptions
            {
                rail = railOptions,
                spawnInterval = railOptions.spawnIntervalSeconds,
            };
        }

        public static PlayableScenarioFeatureOptions FromPhysicsArea(PhysicsAreaFeatureOptions physicsAreaOptions)
        {
            return new PlayableScenarioFeatureOptions
            {
                physicsArea = physicsAreaOptions,
            };
        }
    }
}
