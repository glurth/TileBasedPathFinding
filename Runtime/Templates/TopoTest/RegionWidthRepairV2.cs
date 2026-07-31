using System;
using System.Collections.Generic;
using System.Linq;
using EyE.Maps;
public static class RegionWidthRepairV2
{
    public static Action<string> DebugLog;

    public static void RepairSingleWidthPassages<T>(
        Dictionary<int, HashSet<T>> nodeRegions,
        Random random = null)
        where T : ITileCoordinate<T>
    {
        random ??= new Random();

        HashSet<T> allValidTiles = new HashSet<T>();
        Dictionary<T, int> tileToRegion = new Dictionary<T, int>();
        BuildLookup(nodeRegions, tileToRegion, allValidTiles);

        bool changed = true;

        int iteration = 0;

        while (changed)
        {
            changed = false;

            Log("");
            Log($"===== Iteration {iteration++} =====");

            //
            // Phase 1:
            // remove all one-neighbor spikes repeatedly
            bool changedNub = true;
            while (changedNub)
            {
                changedNub = false;
                List<(T tile, int newRegion)> pending =
                    new List<(T, int)>();

                foreach (T tile in allValidTiles)
                {
                    int region = tileToRegion[tile];

                    Dictionary<int, int> countByRegion = GetNeighborRegionCounts(tile, tileToRegion);
                    int sameCount = 0;
                    countByRegion.TryGetValue(region, out sameCount);

                    if (sameCount < 2) // just getting lone notches now.
                    {
                        int targetRegion = GetHighestNeighborRegion(countByRegion, random);

                        if (targetRegion == region)
                        {
                            //throw new Exception("unexpected targetRegion == region");
                            continue;  //leave it alone
                        }

                        tileToRegion[tile] = targetRegion;
                        nodeRegions[targetRegion].Add(tile);
                        nodeRegions[region].Remove(tile);
                        changedNub = true;
                        changed = true;
                        Log(
                            $"Phase1: {tile} " +
                            $"{region} -> {targetRegion}");
                        break;
                    }
                }
            }
            /*
            //
            // Phase 2:
            //
            List<(T tile, int regionId)>
                passageStarts =
                    new List<(T, int)>();

            foreach ((int regionId, HashSet<T> region)
                in nodeRegions)
            {
                foreach (T tile in region)
                {
                    List<T> sameNeighbors =
                        GetSameRegionNeighbors(
                            tile,
                            region);

                    if (sameNeighbors.Count != 2)
                        continue;

                    //
                    // if neighbors touch each other
                    // not a passage
                    //
                    if (AreNeighbors(
                        sameNeighbors[0],
                        sameNeighbors[1]))
                    {
                        continue;
                    }

                    passageStarts.Add(
                        (tile, regionId));
                }
            }

            HashSet<T> visitedPassages =
                new HashSet<T>();

            foreach ((T startTile, int regionId)
                in passageStarts)
            {
                if (!visitedPassages.Add(
                    startTile))
                {
                    continue;
                }

                HashSet<T> region =
                    nodeRegions[regionId];

                List<T> same =
                    GetSameRegionNeighbors(
                        startTile,
                        region);

                PassageResult<T> left =
                    FollowPassage(
                        startTile,
                        same[0],
                        region,
                        visitedPassages);

                PassageResult<T> right =
                    FollowPassage(
                        startTile,
                        same[1],
                        region,
                        visitedPassages);

                bool leftWidened =
                    left.Widened;

                bool rightWidened =
                    right.Widened;

                List<T> entirePassage =
                    new List<T>();

                entirePassage.AddRange(
                    left.Tiles);

                entirePassage.Add(
                    startTile);

                entirePassage.AddRange(
                    right.Tiles);

                //
                // collapse dead-end passage
                //
                if (!leftWidened ||
                    !rightWidened)
                {
                    Log(
                        $"Collapse: " +
                        $"{startTile}");

                    List<(T, int)> pending =
                        new List<(T, int)>();

                    foreach (T tile in entirePassage)
                    {
                        int newRegion =
                            GetHighestNeighborRegion(
                                tile,
                                tileToRegion,
                                random);

                        pending.Add(
                            (tile, newRegion));
                    }

                    ApplyChanges(
                        pending,
                        nodeRegions,
                        tileToRegion);

                    changed = true;

                    continue;
                }

                //
                // widen
                //
                Log(
                    $"Widen: {startTile}");

                List<(T, int)> additions =
                    BuildWideningStrip(
                        entirePassage,
                        regionId,
                        tileToRegion,
                        random);

                if (additions.Count > 0)
                {
                    ApplyChanges(
                        additions,
                        nodeRegions,
                        tileToRegion);

                    changed = true;
                }
            }
            */
        }//end while changed

    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="nodeRegions"></param>
    /// <param name="tileToRegionToPopulate"></param>
    /// <param name="allValidTilesToPopulate"></param>
    /// <exception> exception thrown if any tile exists in more than one region</exception>
    private static void BuildLookup<T>(Dictionary<int, HashSet<T>> nodeRegions, Dictionary<T, int> tileToRegionToPopulate, HashSet<T> allValidTilesToPopulate)
    {
        Dictionary<T, int> result = new Dictionary<T, int>();

        foreach ((int regionId, HashSet<T> region) in nodeRegions)
        {
            foreach (T tile in region)
            {
                tileToRegionToPopulate[tile] = regionId;
                allValidTilesToPopulate.Add(tile);
            }
        }
        return;
    }

    static Dictionary<int, int> GetNeighborRegionCounts<T>(T tile, Dictionary<T, int> tileToRegion) where T : ITileCoordinate<T>
    {
        Dictionary<int, int> countByRegion = new Dictionary<int, int>();
        foreach (T n in tile.GetSpatialNeighbors())
        {
            if (tileToRegion.TryGetValue(n, out int region))
            {
                if (countByRegion.ContainsKey(region))
                    countByRegion[region]++;
                else
                    countByRegion[region] = 1;
            }
        }
        return countByRegion;
    }

    private static int GetHighestNeighborRegion(Dictionary<int, int> countByRegion, Random random)
    {
        int highestVal = int.MinValue;
        int highestRegion = -1;

        foreach ((int region,int count) in countByRegion)
        {
            if (count >= highestVal)
            {
                highestVal = count;
                highestRegion = region;
            }

        }
        return highestRegion;
    }

    private static void
        Log(
        string s)
    {
        DebugLog?.Invoke(
            $"[RegionWidthRepairV2] {s}");
    }
}