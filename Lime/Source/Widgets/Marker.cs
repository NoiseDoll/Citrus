using System;
using System.Text;
using ProtoBuf;

namespace Lime
{
	[ProtoContract]
	public enum MarkerAction
	{
		[ProtoEnum]
		Play,
		[ProtoEnum]
		Stop,
		[ProtoEnum]
		Jump,
		[ProtoEnum]
		Destroy
	}

	[ProtoContract]
	public class Marker
	{
		[ProtoMember(1)]
		public string Id { get; set; }

		[ProtoMember(2)]
		public int Frame { get; set; }

		public int Time { get { return AnimationUtils.FramesToMsecs(Frame); } }

		[ProtoMember(3)]
		public MarkerAction Action { get; set; }

		[ProtoMember(4)]
		public string JumpTo { get; set; }

		public Action CustomAction { get; set; }

		public Marker() { }

		public Marker(string id, int frame, MarkerAction action, string jumpTo = null)
		{
			this.Id = id;
			this.Frame = frame;
			this.Action = action;
			this.JumpTo = jumpTo;
		}

		public Marker Clone()
		{
			return (Marker)MemberwiseClone();
		}

		public override string ToString()
		{
			return string.Format("{1} '{0}'", Id, Action);
		}
	}
}