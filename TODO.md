## Framework Optimize

### Tools

序列帧拼图工具

### Fixed float

定点数重构

### UI Framework

明确base class， 加载方式，prefab的分离

### InputSystem

加入InputSystem，管理物理按键映射，然后胶水层统一UI和按键的游戏内输入

### Skill System

技能效果基类：各种效果

技能数据类：表达技能效果

技能管理器：存储技能列表，生成技能

技能释放器：创建算法，执行算法

角色状态类：技能效果应用于角色状态

SkillSystem

```bash
|----- common

  	|----- SkillData.cs

  	|-----SkillAttackType.cs

  	|---- SelectorType.cs

|-----CharacterSkillManager # 技能管理器

```

SkillData

```C#
[Serializable] // 可编译器配置
class SkillData
{ 
public: 
  int skillID;
  string name;
  string description;
  int coolTime;
  int coolRemain;
  int costSP;
  float attackDistance;
  string[] attackTargetTags = {"Enemy"};
  [HideInInspector]
  Transform[] attackTargets;
  string[] impactType = {"costSP", "Damage"}; // 技能影响类型
  int nextBattlerId; // 连击的下一个技能的编号
  float atkRatio // 攻击比率
  float durationTime;
  float atkInterval; // 伤害间隔
  [HideInInspector]
  GameObject owner;
  string prefabName;
  [HideInInspector]
  GameObject skillPrefab; // 预制件对象
  string animationName; // 技能动画名
  string hitFxName; // 受击特效名称
  [HideInInspector]
  GameObject hitFxPrefab;
  int level; // 技能等级
  SkillAttackType attackType; // 攻击类型
  SelectorType selectorType;
  
}
```

CharacterSkillManager

```c# 
class CharacterSkillManager
{ 
public: 
  // 技能列表
  SkillData[] skills;
  
	// 准备技能（技能释放条件： 冷却， 法力）
  void SkillData prepareSkill(int id) { 
  	// 查找技能数据
    SkillData data = skills.Find(s => s.skillID == id);
    
    // 判断条件
		float sp = GetComponent<CharacterStatus>().SP;
    if(data != null && data.coolRemain <= 0 && data.coustSP <= sp)
      return data;
    return null;
    // 返回技能数据
  }
  
  // 生成技能
  void GenerateSkill(SkillData data) {
    // 创建技能预制件
  	GameObject skillGo = Instntiate(data.skillPrefab, transform.position, transform.rotation);
    
    // 传递技能数据
    SkillDeployer deployer = skillGo.GetComponet<SkillDeployer>();
    deployer.data = data;
   	deployer.DeploySkill();
    
    // 销毁技能
    Destroy(skillGo, data.durationTime);
    
    // 技能冷却 
    StartCoroutine(CoolTimeDown(SkillGo));
  }
  
private: 
  void Start() { 
  	for(int i = 0;i < skills.Length;i ++) { 
    	InitSkill(skills[i]);
    }
  }
  
  // 初始化技能 
  void InitSkill(SkillData data) { 
    /*
    资源映射表
    资源名称     资源完整路径
    */
    
  	// data.prefabName --> data.skillPrefab
    data.skillPrefab = ResourceManager.Load<GameObject>(data.prefabName);
    data.owner = gameObject;
    
  }
  
  // 技能冷却
  void IEnumerator CoolTimeDown(SkillData data) { 
    data.coolRemain = data.coolTime;
    while(data.coolRemain > 0) { 
            yield return new WaitForSeconds(1);
      data.coolRemain --; 
    }
  }
}
```

调用

```C#
void OnSkillButtonDown() { 
	CharacterSkillManager skillManager = GetComponent<CharacterSkillManager>();
  SkillData data = skillManager.PrepareSkill(1002); // example
  if(data != null) 
    skillManager.GenerateSkill(data);
    
}
```

#### 资源映射表

```C#
class GenerateResConfig : Editor 
{ 
  [MenuItem("Tools/Resources/Generate ResConfig File")]
  public static void Generate() 
  { 
  	// 生成资源配置文件
    string[] resFiles = AssetDatabase.FindAssets("t:prefab", new string[] {"Assets/Resources"});
   	for(int i = 0;i < resFiles.Length;i ++) { 
    	resFiles[i] = AssetDatabase.GUIDToAssetPath(resFiles[i]);
      
      // 对应: 名称=路径
      string fileName = Path.GetFileNameWithoutExtension(resFiles[i]);
      string FilePath = resFiles[i].Replace("Assets/Resources/", string.Empty).Replace(".prefab", string.Empty);
      
      	resFiles[i] = fileName + "=" + FilePath;
    }
    
    File.WriteAllLines("Assets/StreamingAssets/ConfigMap.txt", resFiles);
  	AssetDatabase.Refresh();
  }
  
  // StreamingAssets 目录不会被压缩，可以在移动端读取
}
```

读取资源表

```c#
namespace Common { 
  
class ResourceManager 
{
  private static Dictionary<string, string> configMap;
  
  static ResourceManager() { 
  	string fileContent = GetConfigFile("ConfigMap.txt");
    
    BuildMap(fileContent);
  }
  
  public static string GetConfigFile(string fileName) { 
  	//ConfigMap.txt
    string url;
#if UNITY_EDITOR || UNITY_STANDALONE
    url = "file://" + Application.dataPath + "/StreamingAsstes/" + fileName;
    
#elif UNITY_IPHONE
  	url = "file://" + Application.dataPath + "/Raw/" + fileName;
#elif UNITY_ANDROID
  	url = "jar:file://" + Application.dataPath + "!/assets/" + fileName;
#endif
  
    WWW www = new WWW(url);
    while(true) { 
      if(wwww.isDone) return www.text
    }
  }
  
  public static void BuildMap(string fileContent) { 
  	configMap = new Dictionary<string, string>();
    // todo: 解析
    using (StringReader reader = new StringReader(fileContent)) { 
      string line;
      while((line = reader.ReadLine()) != null) { 
        string line = reader.ReadLine();
        string[] keyValue = line.Split('=');
        configMap.Add(keyvalue[0], keyValue[1]);
      }
			
    }
  }
  
  public static T Load<T>(string prefabName) where T : Object { 
    // name -> path
    string prefabPath = configMap[prefabName];
  	return Resources.Load<T>(prefabPath);
  }
}
  
}

```

SelectorType

```C#
public enum SelectorType
{
  Sector,
  Rectangle
}

public interface IAttackSelector 
{
  Transform[] SelectTarget(SkillData data, Transform skillTF);
}

public c
```

IImpactEffect

```c#
public interface IImpactEffect
{
  void Execute(SkillDeployer data);
}

public class CostSPImpact : IImpactEffect
{
  public void Execute(SkillDeployer deployer) { 
  	var status = deployer.SkillData.owner.GetComponent<CharacterStatus>();
    status.SP -= deployer.SkillData.costSP;
    
  }
}

public class DamageImpact : IImpactEffect
{
  public void Execute(SkillDeployer deployer) { 
  	data = deployer.SkillData;
    deployer.StartCorutine(RepeatDamage());
  }
  
  private IEnumerator RepeatDamage(SkillDeployer deployer) { 
    float atkTime = 0;
  	do{ 
      data = SkillDeployer.SkillData;
      Onceamage(data);
    	yield return new WaitForSeconds(data.atkInterval);
      atkTime += data.atkInterval;
      deployer.CalculateTargets(); 
    } while(atkTime < data.durationTime);
    
  }
  
  private void OnceDamage(SkillData data) { 
  	for(int i = 0;i < data.attackTargets.Length;i ++) { 
      float atk = deployer.SkillData.atkRatio * Dedata.ownr.GetComponet<CharacterStatus>().baseAtk;
    	var status = data.attackTargets[i].GetComponent<CharacterStatus>();
      status.Damage(atk);
    }
  }
}
```

DeployerConfigFactory

```c#
public class DeployerConfigFactory 
{ 
  private static Dictionary<string, object> cache;
  
	public static IAttackSelector CreateAttackSelector(SkillData data) { 
    // 选区对象命名规则
    // 枚举名+AttackSelector
  	string className = string.Format("{0} AttackSelector", data.selectorType)
    // 反射
    return  CreateObject<IAttackSelector>(className);
  }
  
  public static IImpactEffect[] CreateImpactEffects(SkillData data) {
    // 影响
    // 命名：impactType[?] + Impact
    IImpactEffect[] impacts = new IImpacteffect[data.impactType.Length];
  	for(int i = 0;i < data.impactType.Length;i ++) { 
    	string class NameImpact = string Format("{0}Impact", data.impactType[i]);
      impacts[i] = CreateObject<IImpactEffect>(classNameImpact);
    }
    
    return impacts;
  }
  
  private T CreateObject<T>(string className) {
    if(!cache.ContainsKey(calssName)) { 
    	Type type = Type.GetType(className);
    	object instance = Activator.CreateInstance(type);
      cache.Add(className, instance);
    }
  	return cahe[className] as T;
  }
}
```

MeleeSkillDeployer

```c#
public class MeleeSkillDeployer : SkillDeployer
{
  public override void DeploySkill() { 

  	CalculateTargets();
    ImpactTargets();
  }
}
```



SkillDeployer

```c#
class abstract SkillDeployer
{
  private SkillData skillData;
  public SkillData SkillData
  {	
    get { 
    	return skillData;
    }
    set { 
    	skillData = value;
    }
  }
  
  private IAttackSelector selector;
  private IImpactEffet[] impactArray;
  
  private void InitDeployer() { 
  	selector = DeployerConfigFactory.CreateAttackSelector(SkillData);
    impactArray = DeployerConfigFactory.CreateImpactEffects(SkillData);
    
  }
  
  // 执行算法对象
  public void CalculateTargets() { 
  	skillData.attackTargets = selector.SelectTarget(skillData, transform);
  }
  
  public void ImpactTargets() { 
  	for(int i = 0;i < ImpactArray.Length;i ++)
    { 
      // 伤害生命
      impactArrary[i].Execute(this); 
    }
  }
  
  // 释放方式
  public abstract void DeploySkill() { 
  	 
  }
}
```



#### 对象池

```c#
class GameObjectPool : UnitySingletion<GameObjectPool>
{
  private Dictionary<string, List<GameObject>> cache;
  public override void init() { 
    base.init();
    cache = new Dictionary<string, List<GameObject>>();
  }
  
  public gameObject CreateObject(string key, gameObject prefab, Vector3 pos, Quaternion rotate)
  {
    GameObject go = FindUsableObject(key);
    
    if(go == null)
      go = AddObject(key, prefab);
    
    UseObject(pos, rorate, go);
    return go; 
  }
  
  private GameObject AddObject(string key, gameObject prefab) { 
  	GameObject go = instantiate(prefab);
    if(!cache.ContainsKey(key)) cache.Add(key, new List<GameObject()); 
    cache[key].Add(go);
    return go;
  }
  
  private GameObject FindUsableObject(string key) { 
  	if(cache.ContainsKey(key)) { 
    	return cache[key].Find(g => !g.activeInHierarchy); 
      return null;
    }
  }
  
  private void UseObject(vector3 pos, Quaternion rotate, GameObject go) { 
  	go.transform.position = pos;
    go.transform.rotation = rotate;
    go.SetActive(true);
  }
  
  public void CollectObject(GameObject go, float delya = 0) { 
  	StartCoroutine(CollectObject(go, delay));
  }
  
  private IEnumerator void CollectObject(GameObject go, float delay) { 
  	yield return new WaitForSeconds(delay);
  	go.SetActive(false);
  }
  
  public void Clear(string key) { 
  	for(int i = cache[key].Count - 1; i >= 0;i --) { 
    	Destroy(cache[key][i]);
    }	
    
    cache.Remove(key);
  }
  
  public void ClearAll() { 
  	List<string> keyList = new List<string>(cache.Keys);
    foreach(var key in keyList) { 
    	Clear(key);
    }
  }
}
```

CharacterSkillSystem （封装技能）

```c#
class CharacterSkillSystem
{
  private CharacterSkillManager skillManager;
  private Animator anim;
  private void Start() { 
  	skillManager = GetComponet<SkillManager>();
    anim = GetComponentInChildren<Animator>();
  	GetComponetInChildren<AnimationEventBehaviour>().attackHandler += DeploySkill;
  }
  
  private void DeploySkill() { 
  	 skillManager.GenerateSKill(skill);
  }
  
  private SkillData skill;
  
  // 玩家
  public void AttackUseSkill(int skillID, bool isBatter = false) { 
    // 如果连击，从上一个释放的技能获取技能编号
    if(skill != null && isBatter) 
      skillID = skill.nextBatterId；
    
  	skill = skillManager.PrepareSkill(skillID);
    if(skill == null) return;
    anim.SetBool(skill.animationName, true);
    
    if(skill.attackType != SkillAttackType.Single) return ;
    
		Transform targetTF = SelectTarget();
    transform.LookAt(targetTF);
		
    SetSelectedActiveFx(false);
    
    selectedTarget = targetTF;
    SetSelectedActiveFx(true);
  }
  // 准备技能
  // 播放动画
  // 生成技能
  public Transform selectedTarget; 
  
  private Transform SelectTarget() { 
  	Transform[] target = new SectorAttackSelector().SelectTarget(skill, transform);
    return targetLength != 0 ? target[0] : null;
  }
  
  private void SetSelectedActiveFx(bool state) { 
    if(selectedTarget == null) return ; 
  	var selected = targetTF.GetComponent<CharacterSelected>();
    if(selected) selsected.SetSelectedActive(true);
  }
  
  // AI	
  public void UseRandomSkill() { 
  	// 管理器中挑选出随机的技能
    // 先筛选出所有可以释放的技能，产生随机数
    var usablekills = skillManager.skills.FindAll(s => skillManager.PrepareSkill(s.skillID) != null);
    if(usableSkills.Length == 0) return ;
    
    int index = UnityEngine.Random.Range(0, usableSkills.Length);
    AttackUseSkill(usableSkills[index].skillID);
    
  }
}
```

CharacterSelector

```C#
class CharacterSelector
{
  private GameObject selectedGO;
  [Tooltip("选择器物体名称")]
  public string selectedName = "selected";
  [Tooltip("显示时间")]
  public float displayTime = 3;
  
  
  private void Start() { 
  	selectedGO = transform.Find(selectedName).gameObject;
  }
  
  public void SetSelecterActive(bool state) { 
  	selectedGO.SetActive(state);
    this.enabled = state;
    if(state) { 
    	hideTime = Time.time + displayTime;
    }
  }
  
  private void Update()
  {
    if(hideTime <= Time.time) { 
    	SetSelectedActive(false); 
    }
  }
}
```

