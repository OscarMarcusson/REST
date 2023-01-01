using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST
{
	internal class EndPointsManager
	{
		public readonly EndPointsMethodGroup GET = new EndPointsMethodGroup();
		public readonly EndPointsMethodGroup POST = new EndPointsMethodGroup();
	}
}
