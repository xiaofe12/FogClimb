using System;
using System.Collections.Generic;
using UnityEngine;
using Zorro.Core.Serizalization;

namespace Peak.Afflictions
{
	// Token: 0x020003E9 RID: 1001
	public class Affliction_Chaos : Affliction
	{
		// Token: 0x0600199F RID: 6559 RVA: 0x0007E551 File Offset: 0x0007C751
		public Affliction_Chaos()
		{
		}

		// Token: 0x060019A0 RID: 6560 RVA: 0x0007E559 File Offset: 0x0007C759
		public Affliction_Chaos(float statusAmountAverage, float statusAmountStandardDeviation, float averageBonusStamina, float standardDeviationBonusStamina)
		{
			this.statusAmountAverage = statusAmountAverage;
			this.statusAmountStandardDeviation = statusAmountStandardDeviation;
			this.averageBonusStamina = averageBonusStamina;
			this.standardDeviationBonusStamina = standardDeviationBonusStamina;
		}

		// Token: 0x060019A1 RID: 6561 RVA: 0x0007E580 File Offset: 0x0007C780
		public override void OnApplied()
		{
			if (this.character.IsLocal)
			{
				List<CharacterAfflictions.STATUSTYPE> list = new List<CharacterAfflictions.STATUSTYPE>
				{
					CharacterAfflictions.STATUSTYPE.Cold,
					CharacterAfflictions.STATUSTYPE.Hot,
					CharacterAfflictions.STATUSTYPE.Poison,
					CharacterAfflictions.STATUSTYPE.Drowsy,
					CharacterAfflictions.STATUSTYPE.Injury,
					CharacterAfflictions.STATUSTYPE.Hunger,
					CharacterAfflictions.STATUSTYPE.Spores
				};
				this.character.refs.afflictions.ClearAllStatus(false);
				float num = Mathf.Clamp(Util.GenerateNormalDistribution(this.statusAmountAverage, this.statusAmountStandardDeviation), 0f, 1f);
				Debug.Log(string.Format("total status: {0}", num));
				float num2 = num;
				while (num2 > 0.05f && list.Count != 0)
				{
					float num3;
					if (list.Count == 1)
					{
						num3 = num2;
					}
					else
					{
						num3 = num * Util.GenerateNormalDistribution(0.3f, 0.5f);
					}
					Debug.Log(string.Format("Next status: {0}", num3));
					num3 = Mathf.Min(num3, num2);
					if (num3 >= 0.025f)
					{
						int index = Random.Range(0, list.Count);
						CharacterAfflictions.STATUSTYPE statustype = list[index];
						this.character.refs.afflictions.AddStatus(statustype, num3, false, true, true);
						list.RemoveAt(index);
						if (statustype == CharacterAfflictions.STATUSTYPE.Hot)
						{
							list.Remove(CharacterAfflictions.STATUSTYPE.Cold);
						}
						else if (statustype == CharacterAfflictions.STATUSTYPE.Cold)
						{
							list.Remove(CharacterAfflictions.STATUSTYPE.Hot);
						}
						num2 -= num3;
					}
				}
				float extraStamina = Mathf.Clamp(Util.GenerateNormalDistribution(this.averageBonusStamina, this.standardDeviationBonusStamina), 0f, 1f);
				this.character.SetExtraStamina(extraStamina);
				this.character.refs.afflictions.RemoveAffliction(this, false, true);
			}
		}

		// Token: 0x060019A2 RID: 6562 RVA: 0x0007E732 File Offset: 0x0007C932
		public override Affliction.AfflictionType GetAfflictionType()
		{
			return Affliction.AfflictionType.Chaos;
		}

		// Token: 0x060019A3 RID: 6563 RVA: 0x0007E735 File Offset: 0x0007C935
		public override void Stack(Affliction incomingAffliction)
		{
		}

		// Token: 0x060019A4 RID: 6564 RVA: 0x0007E737 File Offset: 0x0007C937
		public override void Serialize(BinarySerializer serializer)
		{
			serializer.WriteFloat(this.statusAmountAverage);
			serializer.WriteFloat(this.statusAmountStandardDeviation);
			serializer.WriteFloat(this.averageBonusStamina);
			serializer.WriteFloat(this.standardDeviationBonusStamina);
		}

		// Token: 0x060019A5 RID: 6565 RVA: 0x0007E769 File Offset: 0x0007C969
		public override void Deserialize(BinaryDeserializer serializer)
		{
			this.statusAmountAverage = serializer.ReadFloat();
			this.statusAmountStandardDeviation = serializer.ReadFloat();
			this.averageBonusStamina = serializer.ReadFloat();
			this.standardDeviationBonusStamina = serializer.ReadFloat();
		}

		// Token: 0x040016DE RID: 5854
		public float statusAmountAverage;

		// Token: 0x040016DF RID: 5855
		public float statusAmountStandardDeviation;

		// Token: 0x040016E0 RID: 5856
		public float averageBonusStamina;

		// Token: 0x040016E1 RID: 5857
		public float standardDeviationBonusStamina;
	}
}
