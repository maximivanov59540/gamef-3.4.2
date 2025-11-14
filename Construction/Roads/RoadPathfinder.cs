using System.Collections.Generic;
using UnityEngine;

/// Гибридный поиск:
/// 1) Сначала пробуем прямую / L-путь (минимум углов, O(d)).
/// 2) Если упёрлись в здание — запускаем A* в ограниченном коридоре
///    вокруг A-B, с приоритетом клеток рядом со зданиями и дорогами.
///    Внутри A* — мин-куча + лимит узлов, чтобы исключить фризы.
public class RoadPathfinder
{
    private readonly GridSystem _grid;

    // Тюнинг (можешь править в рантайме и подбирать)
    private const int   CORRIDOR_MARGIN        = 10;     // «толщина» коридора вокруг A-B
    private const int   NODE_EXPANSION_LIMIT   = 20000;  // страховка от бесконечного поиска
    private const float TURN_PENALTY           = 0.20f;  // штраф за поворот
    private const float ROAD_BONUS             = -0.20f; // бонус по готовой дороге
    private const float NEAR_BUILDING_BONUS    = -0.15f; // бонус клеткам у стен здания

    public RoadPathfinder(GridSystem grid) { _grid = grid; }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var res = new List<Vector2Int>();
        if (start == goal) { res.Add(start); return res; }

        // 1) Быстрый путь без поиска: прямая / два L-варианта
        if (TryStraightOrL(start, goal, out res))
            return res;

        // 2) Узкий коридор вокруг A-B
        BuildCorridor(start, goal, CORRIDOR_MARGIN,
            out int minX, out int minZ, out int maxX, out int maxZ,
            out int W, out int H);

        int Index(int x, int z) => (x - minX) + (z - minZ) * W;
        bool InCorridor(int x, int z) => x >= minX && x <= maxX && z >= minZ && z <= maxZ;

        // Предсканируем коридор (1 проход) — что занято зданиями и где рядом со зданием
        int N = W * H;
        var blocked       = new bool[N];
        var nearBuilding  = new bool[N];

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            int i = Index(x, z);
            blocked[i] = (_grid.GetBuildingIdentityAt(x, z) != null);

            // «рядом со зданием» = любая из 4 соседних клеток — здание
            if (!blocked[i])
            {
                if (_grid.GetBuildingIdentityAt(x + 1, z) != null) nearBuilding[i] = true;
                else if (_grid.GetBuildingIdentityAt(x - 1, z) != null) nearBuilding[i] = true;
                else if (_grid.GetBuildingIdentityAt(x, z + 1) != null) nearBuilding[i] = true;
                else if (_grid.GetBuildingIdentityAt(x, z - 1) != null) nearBuilding[i] = true;
            }
        }

        // A* в коридоре (массивы вместо словарей — быстрее и без лишних аллокаций)
        var gScore  = new float[N];
        var came    = new int[N];
        var cameDir = new sbyte[N]; // 0=up,1=down,2=left,3=right, -1=none

        for (int i = 0; i < N; i++) { gScore[i] = float.PositiveInfinity; came[i] = -1; cameDir[i] = -1; }

        int sIdx = Index(start.x, start.y);
        int gIdx = Index(goal.x,  goal.y);

        if (!InCorridor(start.x, start.y) || !InCorridor(goal.x, goal.y))
            return new List<Vector2Int>(); // что-то странное с коридором

        gScore[sIdx] = 0f;

        // Мин-куча по f=g+h (дубли допускаем — старые записи отбрасываем по gScore)
        var open = new MinHeap();
        open.Push(new PQNode(sIdx, Heuristic(start, goal), 0));

        int expanded = 0;

        // локальные лямбды
        Vector2Int FromIndex(int idx) => new Vector2Int(minX + (idx % W), minZ + (idx / W));

        while (open.Count > 0)
        {
            var node = open.Pop();
            int curIdx = node.idx;

            // «старый» узел — пропускаем
            if (curIdx < 0 || curIdx >= N) continue;

            var curPos = FromIndex(curIdx);
            if (curPos == goal)
                return Reconstruct(came, W, minX, minZ, curIdx);

            if (++expanded > NODE_EXPANSION_LIMIT)
                return new List<Vector2Int>(); // страховка: считаем «пути нет»

            // соседи 4-направления
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = curPos.x, nz = curPos.y;
                switch (dir)
                {
                    case 0: nz += 1; break; // up
                    case 1: nz -= 1; break; // down
                    case 2: nx -= 1; break; // left
                    case 3: nx += 1; break; // right
                }

                if (!InCorridor(nx, nz)) continue;
                int nbIdx = Index(nx, nz);
                if (blocked[nbIdx]) continue; // здание — непроходимо

                float step = 1f;

                // штраф за поворот
                if (cameDir[curIdx] != -1 && cameDir[curIdx] != dir)
                    step += TURN_PENALTY;

                // «приятнее» идти вдоль стен зданий
                if (nearBuilding[nbIdx]) step += NEAR_BUILDING_BONUS;

                // бонус по существующим дорогам
                if (_grid.GetRoadTileAt(nx, nz) != null) step += ROAD_BONUS;

                float tentative = gScore[curIdx] + step;

                if (tentative < gScore[nbIdx])
                {
                    gScore[nbIdx]  = tentative;
                    came[nbIdx]    = curIdx;
                    cameDir[nbIdx] = (sbyte)dir;
                    float f        = tentative + Heuristic(new Vector2Int(nx, nz), goal);
                    open.Push(new PQNode(nbIdx, f, open.Seq++));
                }
            }
        }

        // пути нет
        return new List<Vector2Int>();
    }

    // ───────────────────────── helpers ─────────────────────────

    private static float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static void BuildCorridor(Vector2Int a, Vector2Int b, int margin,
                                      out int minX, out int minZ, out int maxX, out int maxZ, out int W, out int H)
    {
        minX = Mathf.Min(a.x, b.x) - margin;
        maxX = Mathf.Max(a.x, b.x) + margin;
        minZ = Mathf.Min(a.y, b.y) - margin;
        maxZ = Mathf.Max(a.y, b.y) + margin;

        // clamp по размерам сетки
        // (Достаём из любого живого GridSystem — размеры стабильны)
        var anyGrid = Object.FindFirstObjectByType<GridSystem>();
        minX = Mathf.Clamp(minX, 0, anyGrid.GetGridWidth()  - 1);
        maxX = Mathf.Clamp(maxX, 0, anyGrid.GetGridWidth()  - 1);
        minZ = Mathf.Clamp(minZ, 0, anyGrid.GetGridHeight() - 1);
        maxZ = Mathf.Clamp(maxZ, 0, anyGrid.GetGridHeight() - 1);

        W = (maxX - minX + 1);
        H = (maxZ - minZ + 1);
    }

    // Пытаемся без поиска: прямая или две «Г-образные» (минимум углов).
    private bool TryStraightOrL(Vector2Int s, Vector2Int g, out List<Vector2Int> path)
    {
        // прямая по X или Z
        if (s.x == g.x && SegmentClear(s, g))
        { path = DrawManhattanLine(s, g); return true; }
        if (s.y == g.y && SegmentClear(s, g))
        { path = DrawManhattanLine(s, g); return true; }

        // два L-варианта: через (s.x, g.y) и (g.x, s.y)
        var p1 = new Vector2Int(s.x, g.y);
        var p2 = new Vector2Int(g.x, s.y);

        if (SegmentClear(s, p1) && SegmentClear(p1, g))
        { path = DrawManhattanLine(s, p1); path.AddRange(DrawManhattanLine(p1, g, skipFirst:true)); return true; }

        if (SegmentClear(s, p2) && SegmentClear(p2, g))
        { path = DrawManhattanLine(s, p2); path.AddRange(DrawManhattanLine(p2, g, skipFirst:true)); return true; }

        path = null;
        return false;
    }

    // проверка «по клеткам вдоль прямой манхэттеном» на здания
    private bool SegmentClear(Vector2Int a, Vector2Int b)
    {
        int x = a.x, z = a.y;
        int dx = b.x > a.x ? 1 : (b.x < a.x ? -1 : 0);
        int dz = b.y > a.y ? 1 : (b.y < a.y ? -1 : 0);

        while (x != b.x)
        {
            if (_grid.GetBuildingIdentityAt(x, z) != null) return false;
            x += dx;
        }
        while (z != b.y)
        {
            if (_grid.GetBuildingIdentityAt(x, z) != null) return false;
            z += dz;
        }
        // конечная клетка
        return _grid.GetBuildingIdentityAt(b.x, b.y) == null;
    }

    // рисуем клетки вдоль манхэттен-отрезка (для прямой/L)
    private List<Vector2Int> DrawManhattanLine(Vector2Int a, Vector2Int b, bool skipFirst = false)
    {
        var list = new List<Vector2Int>();
        int x = a.x, z = a.y;
        if (!skipFirst) list.Add(new Vector2Int(x, z));

        int dx = b.x > a.x ? 1 : (b.x < a.x ? -1 : 0);
        int dz = b.y > a.y ? 1 : (b.y < a.y ? -1 : 0);

        while (x != b.x) { x += dx; list.Add(new Vector2Int(x, z)); }
        while (z != b.y) { z += dz; list.Add(new Vector2Int(x, z)); }
        return list;
    }

    private List<Vector2Int> Reconstruct(int[] came, int W, int minX, int minZ, int lastIdx)
    {
        var list = new List<Vector2Int>();
        int idx = lastIdx;
        while (idx != -1)
        {
            int lx = minX + (idx % W);
            int lz = minZ + (idx / W);
            list.Add(new Vector2Int(lx, lz));
            idx = came[idx];
        }
        list.Reverse();
        return list;
    }

    // ── простая мин-куча по f ──────────────────────────────────
    private struct PQNode
    {
        public int idx;
        public float f;
        public int seq; // для стабильности
        public PQNode(int idx, float f, int seq) { this.idx = idx; this.f = f; this.seq = seq; }
    }

    private class MinHeap
    {
        private readonly List<PQNode> _data = new List<PQNode>(256);
        public int Seq = 0;
        public int Count => _data.Count;

        public void Push(PQNode n)
        {
            _data.Add(n);
            int i = _data.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (Less(_data[p], _data[i])) break;
                Swap(i, p); i = p;
            }
        }

        public PQNode Pop()
        {
            var top = _data[0];
            int last = _data.Count - 1;
            _data[0] = _data[last];
            _data.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < _data.Count && !Less(_data[m], _data[l])) m = l;
                if (r < _data.Count && !Less(_data[m], _data[r])) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
            return top;
        }

        private static bool Less(PQNode a, PQNode b) => (a.f < b.f) || (Mathf.Approximately(a.f, b.f) && a.seq < b.seq);
        private void Swap(int i, int j) { var t = _data[i]; _data[i] = _data[j]; _data[j] = t; }
    }
}
