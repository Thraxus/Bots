using VRage.Utils;
using VRageMath;

namespace Bots.GridControl.DataTypes
{
	public class PointBillboard
	{
		public MyStringId Material = MyStringId.GetOrCompute("WeaponLaser");
		public Vector4 Color = new Vector4(0, 255, 255, 0.75f);
		public Vector3D Point;
		public float Radius = 2f, Angle = 2;
	}
}
