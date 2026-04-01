/// <summary>
/// 可占领建筑的能力接口。仅步兵/机甲等单位实现此接口后可与建筑交互。
/// </summary>
public interface ICapturable
{
    /// <summary>
    /// 尝试占领指定建筑。返回是否成功。
    /// </summary>
    bool TryCapture(object building);
}
