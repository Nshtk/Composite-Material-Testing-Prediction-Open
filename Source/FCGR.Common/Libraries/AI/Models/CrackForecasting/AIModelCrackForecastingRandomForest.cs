using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using XGBoostSharp;

using FCGR.Common.Utilities;
using SharpLearning.RandomForest.Learners;

namespace FCGR.Common.Libraries.AI.Models.CrackForecasting;

public class AIModelCrackForecastingRandomForest : AIModelBaseSharpLearning<RegressionRandomForestLearner>
{
	public AIModelCrackForecastingRandomForest(string name, int estimators_count, int tree_depth_max, string? log_path = null) : base(name, estimators_count, tree_depth_max, log_path)
	{
	}
}