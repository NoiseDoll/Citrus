using Lime;
using ProtoBuf;
using System.ComponentModel;

namespace Lime
{
	/// <summary>
	/// ������ ����� ����� � ���� �������
	/// </summary>
	[ProtoContract]
	public struct BoneWeight
	{
		/// <summary>
		/// ����� �����
		/// </summary>
		[ProtoMember(1)]
		public int Index;

		/// <summary>
		/// ���� ������� �����
		/// </summary>
		[ProtoMember(2)]
		public float Weight;
	}

	/// <summary>
	/// �������� ���������� � ������� ������� ������ �� ����� DistortionMesh
	/// �������������� ������� 4 ������ ������������
	/// </summary>
	[ProtoContract]
	public class SkinningWeights
	{
		[ProtoMember(1)]
		public BoneWeight Bone0;

		[ProtoMember(2)]
		public BoneWeight Bone1;

		[ProtoMember(3)]
		public BoneWeight Bone2;

		[ProtoMember(4)]
		public BoneWeight Bone3;
	}

	/// <summary>
	/// �����. ��������� ��������� ����� DistortionMesh
	/// </summary>
	[ProtoContract]
	public class Bone : Node
	{
		/// <summary>
		/// ������� � �����
		/// </summary>
		[ProtoMember(1)]
		public Vector2 Position { get; set; }

		/// <summary>
		/// ���� �������� ����� � �������� �� ������� �������
		/// </summary>
		[ProtoMember(2)]
		public float Rotation { get; set; }

		/// <summary>
		/// ����� �����
		/// </summary>
		[ProtoMember(3)]
		public float Length { get; set; }

		/// <summary>
		/// ������������ �������� ����������.
		/// �������� ���������� �� ����� ���������������� �� �����-�������� � �����
		/// </summary>
		[ProtoMember(4)]
		public bool IKStopper { get; set; }

		/// <summary>
		/// ���������� ����� ����� � �����
		/// </summary>
		[ProtoMember(5)]
		public int Index { get; set; }

		/// <summary>
		/// ����� ������������ �����
		/// </summary>
		[ProtoMember(6)]
		public int BaseIndex { get; set; }

		/// <summary>
		/// ������� �������, � ������� ����� ��������� ������������ ������
		/// </summary>
		[ProtoMember(7)]
		public float EffectiveRadius { get; set; }

		/// <summary>
		/// ������� �������, � ������� ����� ��������� ����������� ������
		/// </summary>
		[ProtoMember(8)]
		public float FadeoutZone { get; set; }

		[ProtoMember(9)]
		public Vector2 RefPosition { get; set; }

		[ProtoMember(10)]
		public float RefRotation { get; set; }

		[ProtoMember(11)]
		public float RefLength { get; set; }

		public Bone()
		{
			Length = 100;
			EffectiveRadius = 100;
			FadeoutZone = 50;
			IKStopper = true;
		}

		protected override void SelfLateUpdate(float delta)
		{
			if (Index > 0 && Parent != null) {
				BoneArray.Entry e;
				e.Joint = Position;
				e.Rotation = Rotation;
				e.Length = Length;
				if (BaseIndex > 0) {
					// Tie the bone to the parent bone.
					BoneArray.Entry b = Parent.AsWidget.BoneArray[BaseIndex];
					float l = ClipAboutZero(b.Length);
					Vector2 u = b.Tip - b.Joint;
					Vector2 v = new Vector2(-u.Y / l, u.X / l);
					e.Joint = b.Tip + u * Position.X + v * Position.Y;
					e.Rotation += b.Rotation;
				}
				// Get position of bone's tip.
				e.Tip = Vector2.RotateDeg(new Vector2(e.Length, 0), e.Rotation) + e.Joint;
				if (RefLength != 0) {
					float relativeScaling = Length / ClipAboutZero(RefLength);
					// Calculating the matrix of relative transformation.
					Matrix32 m1, m2;
					m1 = Matrix32.Transformation(Vector2.Zero, Vector2.One, RefRotation * Mathf.DegToRad, RefPosition);
					m2 = Matrix32.Transformation(Vector2.Zero, new Vector2(relativeScaling, 1), e.Rotation * Mathf.DegToRad, e.Joint);
					e.RelativeTransform = m1.CalcInversed() * m2;
				} else
					e.RelativeTransform = Matrix32.Identity;
				Parent.AsWidget.BoneArray[Index] = e;
				Parent.PropagateDirtyFlags(DirtyFlags.Transform);
			}
		}

		static float ClipAboutZero(float value, float eps = 0.0001f)
		{
			if (value > -eps && value < eps)
				return eps < 0 ? -eps : eps;
			else
				return value;
		}
	}
}
