namespace Saltmarsh.Economy
{
    public static class PriceTable
    {
        public const int BulkThreshold = 100;

        // Volume discount applied by harbour traders.
        public static float UnitPriceMultiplier(int units) => units > BulkThreshold ? 0.85f : (units >= 20 ? 0.9f : 1f);
    }
}
