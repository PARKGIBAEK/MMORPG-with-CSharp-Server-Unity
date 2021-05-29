using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Server.Game
{
	public struct Pos
	{
		public Pos(int y, int x) { Y = y; X = x; }
		public int Y;
		public int X;

		public static bool operator==(Pos lhs, Pos rhs)
		{
			return lhs.Y == rhs.Y && lhs.X == rhs.X;
		}

		public static bool operator!=(Pos lhs, Pos rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object obj)
		{
			return (Pos)obj == this;
		}

		public override int GetHashCode()
		{
			long value = (Y << 32) | X;
			return value.GetHashCode();
		}

		public override string ToString()
		{
			return base.ToString();
		}
	}

	public struct PQNode : IComparable<PQNode>
	{
		public int F;
		public int G;
		public int Y;
		public int X;

		public int CompareTo(PQNode other)
		{
			if (F < other.F)
				return 1;
			else if (F > other.F)
				return -1;
			else
				return 0;
			
			//if (F == other.F)
			//	return 0;
			//return F < other.F ? 1 : -1;
		}
	}

	public struct Vector2Int
	{
		public int x;
		public int y;

		public Vector2Int(int x, int y) { this.x = x; this.y = y; }

		public static Vector2Int up { get { return new Vector2Int(0, 1); } }
		public static Vector2Int down { get { return new Vector2Int(0, -1); } }
		public static Vector2Int left { get { return new Vector2Int(-1, 0); } }
		public static Vector2Int right { get { return new Vector2Int(1, 0); } }

		public static Vector2Int operator+(Vector2Int a, Vector2Int b)
		{
			return new Vector2Int(a.x + b.x, a.y + b.y);
		}

		public static Vector2Int operator -(Vector2Int a, Vector2Int b)
		{
			return new Vector2Int(a.x - b.x, a.y - b.y);
		}

		public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
		public int sqrMagnitude { get { return (x * x + y * y); } }
		public int cellDistFromZero { get { return Math.Abs(x) + Math.Abs(y); } }
	}

	public class Map
	{
		public int MinX { get; set; }
		public int MaxX { get; set; }
		public int MinY { get; set; }
		public int MaxY { get; set; }

		public int SizeX { get { return MaxX - MinX + 1; } }
		public int SizeY { get { return MaxY - MinY + 1; } }

		bool[,] _collision;
		GameObject[,] _objects;

		public bool CanGo(Vector2Int cellPos, bool checkObjects = true)
		{
			if (cellPos.x < MinX || cellPos.x > MaxX)
				return false;
			if (cellPos.y < MinY || cellPos.y > MaxY)
				return false;

			int x = cellPos.x - MinX;
			int y = MaxY - cellPos.y;
			return !_collision[y, x] && (!checkObjects || _objects[y, x] == null);
		}

		public GameObject Find(Vector2Int cellPos)
		{
			if (cellPos.x < MinX || cellPos.x > MaxX)
				return null;
			if (cellPos.y < MinY || cellPos.y > MaxY)
				return null;

			int x = cellPos.x - MinX;
			int y = MaxY - cellPos.y;
			return _objects[y, x];
		}

		public bool ApplyLeave(GameObject gameObject)
		{
			if (gameObject.Room == null)
				return false;
			if (gameObject.Room.Map != this)
				return false;

			PositionInfo posInfo = gameObject.PosInfo;
			if (posInfo.PosX < MinX || posInfo.PosX > MaxX)
				return false;
			if (posInfo.PosY < MinY || posInfo.PosY > MaxY)
				return false;

			// Zone
			Zone zone = gameObject.Room.GetZone(gameObject.CellPos);
			zone.Remove(gameObject);

			{
				int x = posInfo.PosX - MinX;
				int y = MaxY - posInfo.PosY;
				if (_objects[y, x] == gameObject)
					_objects[y, x] = null;
			}

			return true;
		}

		public bool ApplyMove(GameObject gameObject, Vector2Int dest, bool checkObjects = true, bool collision = true)
		{
			if (gameObject.Room == null)
				return false;
			if (gameObject.Room.Map != this)
				return false;

			PositionInfo posInfo = gameObject.PosInfo;
			if (CanGo(dest, checkObjects) == false)
				return false;

			if (collision)
			{
				{
					int x = posInfo.PosX - MinX;
					int y = MaxY - posInfo.PosY;
					if (_objects[y, x] == gameObject)
						_objects[y, x] = null;
				}
				{ 
					int x = dest.x - MinX;
					int y = MaxY - dest.y;
					_objects[y, x] = gameObject;
				}
			}

			// Zone
			GameObjectType type = ObjectManager.GetObjectTypeById(gameObject.Id);
			if (type == GameObjectType.Player)
			{
				Player player = (Player)gameObject;
				Zone now = gameObject.Room.GetZone(gameObject.CellPos);
				Zone after = gameObject.Room.GetZone(dest);
				if (now != after)
				{
					now.Players.Remove(player);
					after.Players.Add(player);
				}
			}
			else if (type == GameObjectType.Monster)
			{
				Monster monster = (Monster)gameObject;
				Zone now = gameObject.Room.GetZone(gameObject.CellPos);
				Zone after = gameObject.Room.GetZone(dest);
				if (now != after)
				{
					now.Monsters.Remove(monster);
					after.Monsters.Add(monster);
				}
			}
			else if (type == GameObjectType.Projectile)
			{
				Projectile projectile = (Projectile)gameObject;
				Zone now = gameObject.Room.GetZone(gameObject.CellPos);
				Zone after = gameObject.Room.GetZone(dest);
				if (now != after)
				{
					now.Projectiles.Remove(projectile);
					after.Projectiles.Add(projectile);
				}
			}

			// 실제 좌표 이동
			posInfo.PosX = dest.x;
			posInfo.PosY = dest.y;
			return true;
		}

		public void LoadMap(int mapId, string pathPrefix = "../../../../../Common/MapData")
		{
			string mapName = "Map_" + mapId.ToString("000");

			// Collision 관련 파일
			string text = File.ReadAllText($"{pathPrefix}/{mapName}.txt");
			StringReader reader = new StringReader(text);

			MinX = int.Parse(reader.ReadLine());
			MaxX = int.Parse(reader.ReadLine());
			MinY = int.Parse(reader.ReadLine());
			MaxY = int.Parse(reader.ReadLine());

			int xCount = MaxX - MinX + 1;
			int yCount = MaxY - MinY + 1;
			_collision = new bool[yCount, xCount];
			_objects = new GameObject[yCount, xCount];

			for (int y = 0; y < yCount; y++)
			{
				string line = reader.ReadLine();
				for (int x = 0; x < xCount; x++)
				{
					_collision[y, x] = (line[x] == '1' ? true : false);
				}
			}
		}

		#region A* PathFinding

		// U D L R
		int[] _deltaY = new int[] { 1, -1, 0, 0 };
		int[] _deltaX = new int[] { 0, 0, -1, 1 };
		int[] _cost = new int[] { 10, 10, 10, 10 };

	public List<Vector2Int> FindPath(Vector2Int startCellPos, Vector2Int destCellPos, bool checkObjects = true, int maxDist = 10)
	{
		List<Pos> path = new List<Pos>();

		// 점수 매기기
		// F = G + H
		// F = 최종 점수 (작을 수록 좋음, 경로에 따라 달라짐)
		// G = 시작점에서 해당 좌표까지 이동하는데 드는 비용 (작을 수록 좋음, 경로에 따라 달라짐)
		// H = 목적지에서 얼마나 가까운지 (작을 수록 좋음, 고정)

		// (y, x)에 대한 방문 여부를 확인하기 위함 (closedList에 존재하는 노드 = 방문했던 적이 있음을 의미)
		HashSet<Pos> closedList = new HashSet<Pos>(); // 노드의 방문 여부를 기록

		// (y, x) 가는 길을 한 번이라도 발견했는지
		// 발견X => MaxValue
		// 발견O => F = G + H

		// 노드에 대한 F Cost를 평가하여 기록하는 용도
		// 만약 과거에 평가된 노드일지라도 새로 평가된 F Cost가 더 작다면 F Cost를 갱신한다 ( 경로를 기록하는 parent도 함께 갱신 )
		Dictionary<Pos, int> openedList = new Dictionary<Pos, int>(); // currentNode를 기준으로 탐색한 neighborNode를 저장
		//최적의 경로를 기록하는 용도
		//예를들면 (3,1) -> (3,2) -> (2,2)가 최적의 경로일 경우
		//parent[(2,2)] = (3,2);
		//parent[(3,2)] = (3,1);
		Dictionary<Pos, Pos> parent = new Dictionary<Pos, Pos>();//value 노드는 key 노드로 이동하기 직전 경로(위치)를 의미

		// 오픈리스트에 있는 정보들 중에서, 가장 좋은 후보를 빠르게 뽑아오기 위한 도구
		PriorityQueue<PQNode> pq = new PriorityQueue<PQNode>();//우선 순위는 F Cost가 낮은 것부터

		// CellPos -> ArrayPos
		Pos pos = Cell2Pos(startCellPos);//처음에는 일단 시작노드의 위치를 받아온다
		Pos destination = Cell2Pos(destCellPos);

		// 시작점 입력 (예약 진행)
		openedList.Add(pos, 10 * (Math.Abs(destination.Y - pos.Y) + Math.Abs(destination.X - pos.X)));

		pq.Push(new PQNode() { 
			F = 10 * (Math.Abs(destination.Y - pos.Y) + Math.Abs(destination.X - pos.X)),
			G = 0, Y = pos.Y, X = pos.X }
		);
		parent.Add(pos, pos);//처음 시작 위치이므로 동일 노드끼리 연결

		while (pq.Count > 0)
		{
			// 제일 좋은 후보를 찾는다
			PQNode pqNode = pq.Pop();
			Pos current = new Pos(pqNode.Y, pqNode.X);

			// 이미 방문한 노드일 경우 스킵
			if (closedList.Contains(current))
				continue;

			// 방문한 노드 기록
			closedList.Add(current);

			// 목적지에 도착했으면 탐색 종료
			if (current.Y == destination.Y && current.X == destination.X)
				break;

			//인근 노드 탐색 시작
			// (상하좌우)이동할 수 있는 좌표인지 확인해서 예약(open)한다
			for (int i = 0; i < _deltaY.Length; i++)
			{
				Pos neighbor = new Pos(current.Y + _deltaY[i], current.X + _deltaX[i]);// i값에 따라 상/하/좌/우 탐색

				// 최대 이동거리를 초과할 경우 탐색 스킵
				if (Math.Abs(pos.Y - neighbor.Y) + Math.Abs(pos.X - neighbor.X) > maxDist)
					continue;

				// 유효 범위를 벗어났으면 스킵
				if (neighbor.Y != destination.Y || neighbor.X != destination.X)
				{	// 벽으로 막혀서 갈 수 없으면 스킵
					if (CanGo(Pos2Cell(neighbor), checkObjects) == false) // CellPos
						continue;
				}

				// 이미 방문한 곳이면 스킵
				if (closedList.Contains(neighbor))//※시작점으로 부터 우선순위가 높은 것부터 BFS방식으로 탐색하기 때문에 재방문은 스킵해도 됨
					continue;

				// 현재 경로를 기준으로 현재 노드 평가
				// G Cost : distance from starting node(실제로 평가 된 값)
				// H Cost : distance from target node(추정된 값)
				// F Cost = G Cost + H Cost
				int g = 0;//  pqNode.G + _cost[i]; 맵이 복잡할 경우 사용
				int h = 10 * ((destination.Y - neighbor.Y) * (destination.Y - neighbor.Y) + (destination.X - neighbor.X) * (destination.X - neighbor.X));
					
				int value;
				// NeighborNode에 대한 평가 기록이 있는지 확인
				if (openedList.TryGetValue(neighbor, out value) == false)//TryGetValue 매서드는 value를 복사하여 반환한다.
					value = Int32.MaxValue;//평가 기록이 없을 경우 다른 경로에서 한번도 방문한 적이 없다는 의미
				// 기존에 탐색된 경로가 더 빠를 경우 스킵
				if (value < g + h)
					continue;

				// 평가 기록 예약 진행
				if (openedList.TryAdd(neighbor, g + h) == false)//TryAdd 매서드는 key가 이미 존재할 경우 아무 동작을 하지 않는다
					openedList[neighbor] = g + h;//추가 실패할 경우 key가 이미 존재한다는 것이므로, F Cost만 변경해준다

				pq.Push(new PQNode() { F = g + h, G = g, Y = neighbor.Y, X = neighbor.X });

				if (parent.TryAdd(neighbor, current) == false)
					parent[neighbor] = current;
			}
		}

		return CalcCellPathFromParent(parent, destination);
	}

		List<Vector2Int> CalcCellPathFromParent(Dictionary<Pos, Pos> parent, Pos dest)
		{
			List<Vector2Int> cells = new List<Vector2Int>();

			if (parent.ContainsKey(dest) == false)
			{
				Pos best = new Pos();
				int bestDist = Int32.MaxValue;

				foreach (Pos pos in parent.Keys)
				{
					int dist = Math.Abs(dest.X - pos.X) + Math.Abs(dest.Y - pos.Y);
					// 제일 우수한 후보를 뽑는다
					if (dist < bestDist)
					{
						best = pos;
						bestDist = dist;
					}
				}

				dest = best;
			}

			{
				Pos pos = dest;
				while (parent[pos] != pos)
				{
					cells.Add(Pos2Cell(pos));
					pos = parent[pos];
				}
				cells.Add(Pos2Cell(pos));
				cells.Reverse();
			}

			return cells;
		}

		Pos Cell2Pos(Vector2Int cell)
		{
			// CellPos -> ArrayPos
			return new Pos(MaxY - cell.y, cell.x - MinX);
		}

		Vector2Int Pos2Cell(Pos pos)
		{
			// ArrayPos -> CellPos
			return new Vector2Int(pos.X + MinX, MaxY - pos.Y);
		}

		#endregion A* PathFinding
	}

}
