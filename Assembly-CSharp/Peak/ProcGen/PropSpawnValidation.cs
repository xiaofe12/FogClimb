using System;
using System.Collections.Generic;
using UnityEngine;

namespace Peak.ProcGen
{
	// Token: 0x020003D6 RID: 982
	public static class PropSpawnValidation
	{
		// Token: 0x0600192E RID: 6446 RVA: 0x0007D44C File Offset: 0x0007B64C
		public static Color GetValidationColorImpl(this IValidatable self)
		{
			return PropSpawnValidation.ValidationColors[self.ValidationState];
		}

		// Token: 0x040016C5 RID: 5829
		public static readonly Dictionary<ValidationState, Color> ValidationColors = new Dictionary<ValidationState, Color>
		{
			{
				ValidationState.Unknown,
				Color.yellow
			},
			{
				ValidationState.Passed,
				Color.green
			},
			{
				ValidationState.Failed,
				Color.red
			}
		};
	}
}
