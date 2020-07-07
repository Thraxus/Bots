using VRage.Utils;
using VRageMath;

namespace Bots.GridControl.DataTypes
{
	public class Line
	{
		// VRageMath.Color has defaults, not Vector4 though (r, g, b, a), just (r, g, b)
		// WeaponLaser
		//public Vector3D From;
		//public Vector3D To;
		//public MyStringId? Material = MyStringId.GetOrCompute("particle_laser");
		//public MyStringId? Material = MyStringId.GetOrCompute("WeaponLaser");
		//public Vector4 Color = new Vector4(0, 255, 128, 0.75f);
		//public float Thickness = 1f;

		public MyStringId Material = MyStringId.GetOrCompute("WeaponLaser");
		public Vector4 Color = new Vector4(0, 255, 128, 0.75f);
		public Vector3D From;
		public Vector3D To;

		public float Thickness = 1f;
		public int CustomViewProjection = -1;
		public float Intensity = 1;

		public Vector3 DirectionNormalized;
		public float Length;

		public void Set()
		{
			Length = (float) Vector3D.Distance(From, To);
			DirectionNormalized = Vector3.Normalize(To - From);
		}

		//public BlendTypeEnum blendType = BlendTypeEnum.Standard;

		//public List<MyBillboard> persistentBillboards = null;
	}
}
