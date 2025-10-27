using Vintagestory.API.Datastructures;

namespace Polaris;

[CollectibleBehavior]
public class RawFoodBehavior : CollectibleBehavior
{
    public FoodNutritionProperties? NutritionalProps { get; private set; }

    public RawFoodBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        NutritionalProps = properties["props"].AsObject<FoodNutritionProperties>(null!);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        int t = 1;
    }
}