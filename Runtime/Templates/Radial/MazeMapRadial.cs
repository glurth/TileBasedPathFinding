using System.Collections.Generic;
using UnityEngine;

namespace EyE.Maps.Templates
{
    /// <summary>
    /// A radial/concentric-ring maze. Each ring can have an increasing number of sectors so
    /// that outer rings can contain more tiles (commonly used in radial mazes).
    /// 
    /// Configuration:
    /// - rings: number of rings (ring 0 is the center tile)
    /// - baseSectors: number of sectors on ring 1 (minimum 1)
    /// - sectorsStep: additional sectors added per ring step (linear growth)
    /// - worldScale controls spacing (passed to GenericMazeMap)
    /// 
    /// This map will configure RadialCoord static parameters on construction so
    /// RadialCoord.GetNeighbor/heuristic/mapping behave consistently for the map.
    /// </summary>
    public class MazeMapRadial : GenericMazeMap<RadialCoord>
    {
        private int rings;
        internal int baseSectors;


        public MazeMapRadial(int rings, int baseSectors = 6, float worldScale = 1f)
            : base(new RadialCoord(rings, baseSectors), new RadialCoord(0, 0), new RadialCoord(rings - 1, 0), worldScale)
        {

            this.rings = Mathf.Max(1, rings);
            this.baseSectors = Mathf.Max(1, baseSectors);
            RadialCoord.mapRef = this;
        }


        public int SectorsAtRing(int ringIndex)
        {
            if (ringIndex <= 0) return 1;

            float r1 = worldScale;
            float r = ringIndex * worldScale;

            int increases = (int)Mathf.Floor(Mathf.Log(r / r1, 2.0f));

            if (increases < 0)
                increases = 0;
            
           // return (int)(baseSectors * Mathf.Pow(RadialCoord.sectorIncreaseFactor, increases));
            return baseSectors << increases;
        }

        public override IEnumerable<RadialCoord> allMapCoords
        {
            get
            {
                for (int r = 0; r < rings; r++)
                {
                    int sectors = SectorsAtRing(r);
                    for (int s = 0; s < sectors; s++)
                    {
                        yield return new RadialCoord(r, s);
                    }
                }
            }
        }

        public float RingInnerRadius(int ring)
        {
            return ring * worldScale;
        }
        public float RingCenterRadius(int ring)
        {
            return (ring+.5f) * worldScale;
        }
        public float RingOuterRadius(int ring)
        {
            return (ring+1) * worldScale;
        }

        // Get model-space (3D) position for the center of a tile. We place tiles on XZ plane, Y=0.
        public override Vector3 GetModelSpacePosition(RadialCoord coord)
        {
            if (coord.ring == 0) return Vector3.zero;

            float radius = RingCenterRadius(coord.ring);//. + 0.5f) * worldScale; // radial spacing uses worldScale as unit
            
           // float angle = coord.AngleInTurns * Mathf.PI * 2f;
            Vector2 posOnPlane = coord.radialDirection*radius;// Vector2Extensions.NormalFromAngle(angle)* radius;
            return new Vector3(posOnPlane.x, posOnPlane.y,0);
        }

        public override Vector3 SingleTileModelSpaceOffset()
        {
            // approximate offset between tile (0,0) and (1,0) (i.e., radial increment)
            Vector3 a = GetModelSpacePosition(new RadialCoord(0, 0));
            Vector3 b = GetModelSpacePosition(new RadialCoord(1, 0));
            return b - a;
        }

        // simple bounds check
        public override bool IsWithinBounds(RadialCoord coord)
        {
            if (coord.ring < 0 || coord.ring >= rings) return false;
            int sectors = SectorsAtRing(coord.ring);
            if (coord.sector < 0 || coord.sector >= sectors) return false;
            return true;
        }
        
        // Orientation for a border between coord and neighborIndex: rotate so +X points across the edge
        public override Quaternion NeighborBorderOrientation(RadialCoord coord, int neighborIndex)
        {
            // Find neighbor and use the midpoint angle between the two tile centers to orient the wall
            RadialCoord neighbor = coord.GetNeighbor(neighborIndex);


            Vector3 a = GetModelSpacePosition(coord);
            Vector3 b = GetModelSpacePosition(neighbor);
            if (neighbor.ring == coord.ring)
            {
                float avgTurns = (coord.AngleInTurns + neighbor.AngleInTurns)*0.5f;
                return Quaternion.Euler(0, 0,90+ avgTurns * 360);
            }
            //if we get here we are on different rings.
            //we will use the OUTER ring's coordinate to compute wall angle.
            if (neighbor.ring > coord.ring)
                return Quaternion.Euler(0, 0,90 + neighbor.AngleInTurns * 360);
            return Quaternion.Euler(0, 0,  coord.AngleInTurns * 360);
        }


        override public Bounds GetModelSpaceBounds()
        {
            Bounds bounds;
            float r = RingOuterRadius(rings-1) * 2f;
            bounds = new Bounds(Vector3.zero, new Vector3(r,r,0));
            return bounds;
        }
    }
}