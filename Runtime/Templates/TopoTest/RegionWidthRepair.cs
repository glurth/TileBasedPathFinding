using System;
using System.Collections.Generic;
using System.Text;
using EyE.Maps;


/*to try:  for each tile, count the number of neighbors in each different region.

if this tile has only one neighbor of the same region, change it's region to the  highest neighbor count region.
if this tile has exactly two neighbros of the same region.
  if those two neighborsn also neighbro ech other, no change.
  if they are not... we have a single tile width passage.
      follow passage in each direction until it widens
        if it never widens in even one direction, close off entire passage in that direction, and close it off till it widens in the other direction
            change each passage-tile's region to it's highest neighbor count region.
        if it does widen out:
          NOW increase width of single width passge, by converting one valid neighboring tile of each step  in the single width passage to this region.  The steps added in this way, should be neighbors with each other (used to select WHICH neighbor we expand into).
          
repeat until no tiles changed.

*/
public static class OLDRegionWidthRepair
{
   
    public static Action<string> DebugLog = null;

    private struct Choke<T>
        where T : ITileCoordinate<T>
    {
        public int RegionId;
        public T Tile;

        public Choke(
            int regionId,
            T tile)
        {
            RegionId = regionId;
            Tile = tile;
        }

        public override string ToString()
        {
            return $"Region:{RegionId} Tile:{Tile}";
        }
    }

    public static void RepairSingleWidthPassages<T>(
        Dictionary<int, HashSet<T>> nodeRegions,
        int maxIterations = 8)
        where T : ITileCoordinate<T>
    {
        OLDRegionWidthRepair.DebugLog = s => UnityEngine.Debug.Log(s);

        Log("");
        Log("========================================");
        Log("RegionWidthRepair BEGIN");
        Log("========================================");

        ValidateRegionMap(nodeRegions);

        Dictionary<T, int> tileToRegion =
            BuildTileLookup(nodeRegions);

        Log($"Regions: {nodeRegions.Count}");
        Log($"Total Tiles: {tileToRegion.Count}");

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Log("");
            Log($"----- Iteration {iteration} -----");

            List<Choke<T>> chokes =
                FindAllChokes(nodeRegions);

            Log($"Detected choke count: {chokes.Count}");

            foreach (Choke<T> choke in chokes)
            {
                Log($"Choke: {choke}");
            }

            if (chokes.Count == 0)
            {
                Log("No remaining chokes.");
                return;
            }

            bool repairedAny = false;

            for (int i = 0; i < chokes.Count; i++)
            {
                Choke<T> choke =
                    chokes[i];

                HashSet<T> region =
                    nodeRegions[choke.RegionId];

                bool stillChoke =
                    IsArticulationTile(
                        region,
                        choke.Tile);

                Log(
                    $"Testing choke " +
                    $"{choke.Tile} " +
                    $"stillArticulation:{stillChoke}");

                if (!stillChoke)
                    continue;

                bool repaired =
                    TryRepairChoke(
                        choke.RegionId,
                        choke.Tile,
                        nodeRegions,
                        tileToRegion);

                Log(
                    $"Repair result: " +
                    $"{repaired}");

                if (repaired)
                {
                    repairedAny = true;
                }
            }

            if (!repairedAny)
            {
                Log("");
                Log("No repairs performed.");
                Log("Stopping.");
                return;
            }
        }

        Log("Max iterations reached.");
    }

    private static void ValidateRegionMap<T>(
        Dictionary<int, HashSet<T>> nodeRegions)
        where T : ITileCoordinate<T>
    {
        HashSet<T> seen =
            new HashSet<T>();

        foreach ((int regionId, HashSet<T> region)
            in nodeRegions)
        {
            foreach (T tile in region)
            {
                if (!seen.Add(tile))
                {
                    throw new InvalidOperationException(
                        $"Duplicate tile ownership: {tile}");
                }
            }
        }
    }

    private static Dictionary<T, int> BuildTileLookup<T>(
        Dictionary<int, HashSet<T>> nodeRegions)
        where T : ITileCoordinate<T>
    {
        Dictionary<T, int> lookup =
            new Dictionary<T, int>();

        foreach ((int regionId, HashSet<T> region)
            in nodeRegions)
        {
            foreach (T tile in region)
            {
                lookup.Add(
                    tile,
                    regionId);
            }
        }

        return lookup;
    }

    private static List<Choke<T>> FindAllChokes<T>(
        Dictionary<int, HashSet<T>> nodeRegions)
        where T : ITileCoordinate<T>
    {
        List<Choke<T>> result =
            new List<Choke<T>>();

        foreach ((int regionId, HashSet<T> region)
            in nodeRegions)
        {
            HashSet<T> articulationTiles =
                FindArticulationTiles(region);

            Log(
                $"Region {regionId}: " +
                $"{articulationTiles.Count} articulation tiles");

            foreach (T tile in articulationTiles)
            {
                result.Add(
                    new Choke<T>(
                        regionId,
                        tile));
            }
        }

        return result;
    }

    private static bool TryRepairChoke<T>(
        int regionId,
        T chokeTile,
        Dictionary<int, HashSet<T>> nodeRegions,
        Dictionary<T, int> tileToRegion)
        where T : ITileCoordinate<T>
    {
        Log("");
        Log($"Repairing choke {chokeTile}");

        HashSet<T> region =
            nodeRegions[regionId];

        int originalChokeCount =
            CountArticulationTiles(region);

        Log(
            $"Original choke count: " +
            $"{originalChokeCount}");


        List<T> candidates = GetExpansionCandidatesRadius2(chokeTile,region,tileToRegion);

        for (int i = 0; i < candidates.Count; i++)
        {
            T candidate =
                candidates[i];

            Log(
                $"Candidate neighbor: " +
                $"{candidate}");

            if (region.Contains(candidate))
            {
                Log("Rejected: already in region");
                continue;
            }

            if (!tileToRegion.TryGetValue(
                candidate,
                out int donorRegionId))
            {
                Log(
                    "Rejected: neighbor not " +
                    "owned by any region");
                continue;
            }

            if (!nodeRegions[donorRegionId]
                .Contains(candidate))
            {
                throw new InvalidOperationException(
                    $"tileToRegion desync: {candidate}");
            }

            HashSet<T> donorRegion =
                nodeRegions[donorRegionId];

            if (donorRegion.Count <= 1)
            {
                Log(
                    "Rejected: donor region " +
                    "would collapse");
                continue;
            }

            Log(
                $"Simulating transfer from " +
                $"region {donorRegionId}");

            donorRegion.Remove(candidate);

            region.Add(candidate);

            tileToRegion[candidate] =
                regionId;

            bool success =
                EvaluateRepairMove(
                    region,
                    donorRegion,
                    chokeTile,
                    candidate,
                    originalChokeCount);

            Log(
                $"Simulation result: " +
                $"{success}");

            if (success)
            {
                Log(
                    $"SUCCESS: moved {candidate}");
                return true;
            }

            region.Remove(candidate);

            donorRegion.Add(candidate);

            tileToRegion[candidate] =
                donorRegionId;

            Log("Reverted simulation");
        }

        Log("No valid candidate found");

        return false;
    }

    private static bool EvaluateRepairMove<T>(
        HashSet<T> region,
        HashSet<T> donorRegion,
        T originalChoke,
        T addedTile,
        int originalChokeCount)
        where T : ITileCoordinate<T>
    {
        if (!IsConnected(donorRegion))
        {
            Log(
                "Rejected: donor disconnected");
            return false;
        }

        if (IsArticulationTile(
            region,
            addedTile))
        {
            Log(
                "Rejected: moved choke");
            return false;
        }
        return true;
    }

    private static List<T> GetExpansionCandidatesRadius2<T>(
                T chokeTile,
                HashSet<T> region,
                Dictionary<T, int> tileToRegion)
                where T : ITileCoordinate<T>
    {
        HashSet<T> visited =
            new HashSet<T>();

        Queue<(T tile, int depth)> queue =
            new Queue<(T, int)>();

        List<T> result =
            new List<T>();

        queue.Enqueue((chokeTile, 0));
        visited.Add(chokeTile);

        while (queue.Count > 0)
        {
            (T tile, int depth) = queue.Dequeue();

            if (depth >= 2)
                continue;

            T[] neighbors = tile.GetSpatialNeighbors();

            for (int i = 0; i < neighbors.Length; i++)
            {
                T n = neighbors[i];

                if (!visited.Add(n))
                    continue;

                //
                // Only consider tiles not already in region
                //
                if (!region.Contains(n))
                { 
                    if (tileToRegion.ContainsKey(n))//is in any region/valid tile coord
                    {
                        result.Add(n);
                    }
                }
                else
                    if (tileToRegion.ContainsKey(n))
                        queue.Enqueue((n, depth + 1));
            }
        }

        return result;
    }

    private static int CountArticulationTiles<T>(
        HashSet<T> region)
        where T : ITileCoordinate<T>
    {
        return FindArticulationTiles(region)
            .Count;
    }

    private static HashSet<T> FindArticulationTiles<T>(
        HashSet<T> region)
        where T : ITileCoordinate<T>
    {
        HashSet<T> result =
            new HashSet<T>();

        //
        // Snapshot first because IsArticulationTile()
        // temporarily mutates region.
        //
        List<T> tiles =
            new List<T>(region);

        for (int i = 0; i < tiles.Count; i++)
        {
            T tile =
                tiles[i];

            if (IsArticulationTile(
                region,
                tile))
            {
                result.Add(tile);
            }
        }

        return result;
    }

    private static bool IsArticulationTile<T>(
        HashSet<T> region,
        T tile)
        where T : ITileCoordinate<T>
    {
        if (!region.Contains(tile))
            return false;

        region.Remove(tile);

        bool connected =
            IsConnected(region);

        region.Add(tile);

        return !connected;
    }

    private static bool IsConnected<T>(
        HashSet<T> region)
        where T : ITileCoordinate<T>
    {
        if (region.Count <= 1)
            return true;

        using IEnumerator<T> e =
            region.GetEnumerator();

        e.MoveNext();

        T start =
            e.Current;

        HashSet<T> visited =
            new HashSet<T>();

        Queue<T> queue =
            new Queue<T>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            T current =
                queue.Dequeue();

            T[] neighbors =
                current.GetSpatialNeighbors();

            for (int i = 0; i < neighbors.Length; i++)
            {
                T neighbor =
                    neighbors[i];

                if (!region.Contains(neighbor))
                    continue;

                if (!visited.Add(neighbor))
                    continue;

                queue.Enqueue(neighbor);
            }
        }

        return visited.Count ==
               region.Count;
    }

    static string logStr="";
    private static void Log(
        string text)
    {
        logStr += "\n" + text;
        if (logStr.Length > 800)
        {
            DebugLog?.Invoke(logStr);
            logStr = "";
        }
    }
}