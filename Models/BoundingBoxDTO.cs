using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
	public class BoundingBoxDTO
	{
		public int Id { get; set; }
		public int EventId { get; set; }
		public float X1 { get; set; }
		public float Y1 { get; set; }
		public float Width { get; set; }
		public float Height { get; set; }
		public string? Label { get; set; }
		public float Probability { get; set; }
	}
}
