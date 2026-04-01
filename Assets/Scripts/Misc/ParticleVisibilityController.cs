using UnityEngine;

public class ParticleVisibilityController : MonoBehaviour
{
    private ParticleSystem pt;
    private Vector3 lastPosition;
    
    [Header("设置")]
    [Tooltip("移动速度阈值，超过这个值认为在移动")]
    public float moveThreshold = 0.01f;
    
    [Tooltip("勾选：移动时瞬间清空所有粒子；不勾选：只停止发射新粒子")]
    public bool clearInstantly = false;

    void Start()
    {
        // 获取本物体或子物体上的粒子系统（Mark 等预制体常把粒子放在子节点）
        pt = GetComponentInChildren<ParticleSystem>();
        lastPosition = transform.position;
    }

    void Update()
    {
        if (pt == null)
            return;

        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        var emission = pt.emission;

        // 判断是否在移动
        if (distanceMoved > moveThreshold)
        {
            // --- 正在移动 ---
            if (emission.enabled)
            {
                emission.enabled = false; // 关闭发射器
            }
            
            // 如果勾选了瞬间消失，直接清空屏幕上的粒子
            if (clearInstantly)
            {
                pt.Clear(); 
            }
        }
        else
        {
            // --- 处于静止 ---
            if (!emission.enabled)
            {
                emission.enabled = true; // 打开发射器
                
                // 如果粒子系统停了，重新唤醒它
                if (!pt.isPlaying)
                {
                    pt.Play();
                }
            }
        }

        // 更新位置记录，供下一帧判断
        lastPosition = transform.position;
    }
}