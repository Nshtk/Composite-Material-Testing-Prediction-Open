using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FCGR.Common.Libraries;

namespace CMTP.Avalonia.Models;

public class Material : Model
{
	public enum TYPE
	{
		[Description("Ткань")]
		FABRIC,
		[Description("Жгут")]
		WISP
	}
	public enum REINFORCEMENT_TYPE
	{
		[Description("Ноль")]
		ZERO,
		[Description("Квази")]
		QUASI
	}
	public enum CLIMATE_CONDITIONS
	{
		[Description("Нет")]
		BASE,
		[Description("ГЦКИ")]
		GCKI,
		[Description("КНР")]
		PRC
	}

	private TYPE _type = TYPE.WISP;
	private REINFORCEMENT_TYPE _reinforcement_type = REINFORCEMENT_TYPE.QUASI;
	private CLIMATE_CONDITIONS _climate_conditions = CLIMATE_CONDITIONS.BASE;
	private float _E_c = 20.1f, _tau_M = 100, _h__p = 0.215f, _tau__12M = 93, _G__12 = 4.8f, _eps = 2.3f, _mu = 0.25f, _V = 38, _T__gdry = 185f, _climate_conditions_time_spent = 0f;
	private int _sigma_C_B = 670, _E = 36, _s__max = 580, _sigma__B_min = 787, _sigma__B_max = 905, _sigma__B_mean = 854;

	public TYPE Type
	{
		get { return _type; }
		set { _type = value; }
	}
	public REINFORCEMENT_TYPE Reinforcement_Type
	{
		get { return _reinforcement_type; }
		set { _reinforcement_type = value; }
	}
	public CLIMATE_CONDITIONS Climate_Conditions
	{
		get { return _climate_conditions; }
		set { _climate_conditions = value; }
	}
	public float E_C
	{
		get { return _E_c; }
		set { _E_c = value; OnPropertyChanged(); }
	}
	public float Tau_M
	{
		get { return _tau_M; }
		set { _tau_M = value; OnPropertyChanged(); }
	}
	public float Tau__12M
	{
		get { return _tau__12M; }
		set { _tau__12M = value; OnPropertyChanged(); }
	}
	public float G__12
	{
		get { return _G__12; }
		set { _G__12 = value; OnPropertyChanged(); }
	}
	public float Eps
	{
		get { return _eps; }
		set { _eps = value; OnPropertyChanged(); }
	}
	public float Mu
	{
		get { return _mu; }
		set { _mu = value; OnPropertyChanged(); }
	}
	public float V
	{
		get { return _V; }
		set { _V = value; OnPropertyChanged(); }
	}
	public float T__Gdry
	{
		get { return _T__gdry; }
		set { _T__gdry = value; OnPropertyChanged(); }
	}
	public float Climate_Conditions_Time_Spent
	{
		get { return _climate_conditions_time_spent; }
		set { _climate_conditions_time_spent = value; OnPropertyChanged(); }
	}
	public int Sigma_C_B
	{
		get { return _sigma_C_B; }
		set { _sigma_C_B = value; }
	}
	public int E
	{
		get { return _E; }
		set { _E = value; }
	}
	public float H__P
	{
		get { return _h__p; }
		set { _h__p = value; OnPropertyChanged(); }
	}
	public int S__Max
	{
		get { return _s__max; }
		set { _s__max = value; OnPropertyChanged(); }
	}
	public int Sigma__B_Min
	{
		get { return _sigma__B_min; }
		set { _sigma__B_min = value; OnPropertyChanged(); }
	}
	public int Sigma__B_Max
	{
		get { return _sigma__B_max; }
		set { _sigma__B_max = value; OnPropertyChanged(); }
	}
	public int Sigma__B_Mean
	{
		get { return _sigma__B_mean; }
		set { _sigma__B_mean = value; OnPropertyChanged(); }
	}

	public Material()
	{

	}
}
