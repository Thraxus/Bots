using System;
using System.Collections.Generic;
using VRage.Collections;
using VRageMath;

namespace Bots.GridControl.Models
{
	public class PatrolRoute
	{
		private readonly List<Vector3D> _route = new List<Vector3D>()
		{
			new Vector3D(-43229.021168609252, -9269.5177582804135, 43489.220552603845),
			new Vector3D(-42215.248862785862, -10106.238471936351, 44052.730770789916),
			new Vector3D(-41429.448022182289, -9966.6928187007179, 45368.193750827246),
			new Vector3D(-41763.776891319809, -8156.2750849056138, 45627.912503207961),
			new Vector3D(-42359.304601329386, -6601.9854178186824, 43311.280304341512),
			new Vector3D(-44799.281991765121, -7653.6194506676993, 42228.293961479911),
			new Vector3D(-43120.573476366189, -9248.4646031342054, 42960.09875162087)
		};

		private int _index = 0;

		public void AddNode(Vector3D node)
		{
			_route.Add(node);
		}

		public void RemoveNode(Vector3D node)
		{
			_route.Remove(node);
		}

		public void ClearRoute()
		{
			_route.Clear();
		}

		public Vector3D GetNextNode()
		{
			if (_index > _route.Count - 1) _index = 0;
			return _route[_index++];
		}
	}
}
