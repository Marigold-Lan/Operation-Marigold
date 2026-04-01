import os
import time
import subprocess
import re  # 引入正则库，用于提取纯净的文档内容

# 1.技术点映射表
tech_mapping = {
    
}

# 2.全局项目设定 (保持你的完美设定不变)
GLOBAL_CONTEXT = """
【项目全局设定：Operation Marigold】
# 1. 定位
致敬《Advance Wars》的 3D 回合制战棋 Demo。核心玩法：网格上的移动、战斗、占领与回合经济；AI 系统是重点投入方向之一。
# 2. 技术栈
Unity + C#，逻辑以 Grid（网格坐标） 为基准做移动、射程、占领、寻路等演算。
具体分析某脚本时，以该文件及同级/上下游依赖为准；本段只约定分层与习惯，不替代代码细节。
# 3. 架构原则
- 组件化：单位等行为拆成相对独立的职责（如移动、战斗、运输等），避免单个巨型 MonoBehaviour 承载全部玩法。
- 数据/规则 vs 表现：玩法状态与规则校验尽量与动画、特效、UI 解耦；表现层通过事件或 Facade 读状态，避免在 Update 里大范围轮询「猜世界变了没」。
- 命令与规则：玩家与 AI 宜走同一套命令（Command）入口；合法性、结算放在 Rules / 共享校验里，避免 AI 分支一套规则、玩家另一套规则。
- 事件驱动：用 C# 事件、ScriptableObject 事件或其它项目内既定方式驱动 UI、音频、特效；订阅与取消订阅成对（如 OnEnable / OnDisable），减少泄漏与重复回调。
- 输入：WASD操控游标在地图与菜单上移动，也有专门的确认与返回键。而不是用鼠标点击。
# 4. 性能与工程习惯（战棋场景）
- 热路径：寻路、每格可达性、邻格查询优先 O(1) 或近似常数 的结构（预计算、缓存、字典/数组索引），避免在循环里 LINQ、反复 Find、全图扫描。
- 关注 GC：高频路径少分配（少闭包捕获大对象、注意装箱、避免每帧 new 集合）；必要时对象池、复用缓冲区。
- 不要求「零 Update」，而是：逻辑不依赖每帧轮询；表现用事件、动画状态或有限次的驱动即可。
# 5. AI 与模拟边界
- AI 输出意图或命令（与玩家操作同构），胜负与数值结算仍以规则层为准。
"""

# 3. 注入了“强制思维链”的终极 Prompt
USER_PROMPT_TEMPLATE = """
【角色设定】
你现在是一位经验丰富的游戏客户端主程，正在为你的项目核心技术点撰写复盘文档与面试准备材料。你的语气需要专业、自信、务实，既懂底层原理，又懂架构上的 Trade-off（权衡）。

【项目全局设定：Operation Marigold】
{global_context}

简历上的技术点是：【{tech_point}】
以下是我该技术点的核心底层源码：
{code_content}

【强制深度推演指令（Chain of Thought）】
为了确保你能给出最深刻、最符合大厂面试标准的回答，你必须在输出正式文档前，先在一个 <thinking> 标签内进行沙盘推演。
在 <thinking> 中，你需要仔细思考并写下：
1. 逐个文件分析：这些类是怎么串联起来的？数据流向是怎样的？
2. 寻找痛点与妥协（Trade-off）：这段代码里有没有为了性能（GC、O(1)查询）而牺牲可读性的地方？有没有潜在的局限性？
3. 换位思考：如果我是腾讯/网易/米哈游的严苛面试官，看到这个技术点，我会从哪些极端边界和底层原理去刁难候选人？
只有完成深度推演后，再将最终的 Markdown 文档输出在 <document> 标签内。

【正式文档输出格式与内容要求】
在 <document> 标签内，请严格按照以下 4 个模块输出，标题必须一字不差地保留。

# {tech_point}

## 1. 架构概览：它在项目中扮演什么角色？
* 必须结合项目设定，列举至少 3 个依赖该技术点的上层系统（如寻路、交互、AI等），强调其作为基础设施的重要性。

## 2. 核心实现路径：我是怎么做的？
* 分步解析（使用“第一步：xxx”、“第二步：xxx”）。不仅要说“怎么做的”，更要解释“为什么用这个特定的 API/数据结构/数学计算”。提取核心的几行关键代码即可，绝对不要大段粘贴源码。

## 3. 方案对比与架构反思：为什么这么做？
* 竞品对比：列举至少 2 种常规做法或引擎自带方案，并指出它们在此项目中的致命缺点。
* 局限与优化：提出当前方案在面对更极端情况（如超大地图 1000x1000、非规则形状、高频调用瓶颈）时的局限性，并给出具体的未来优化方向（如空间分块、延迟加载、SIMD 优化等）。

## 4. Q&A
* 模拟 3 个大厂面试官的提问及第一人称的优秀回答（面试官绝对没看过源码，只根据名词提问而不是盯着api）。
* 第一个问题（基础原理）：针对实现细节和极端边界情况提问。
* 第二个问题（深度追问）：针对地图变大、性能瓶颈、内存分配提问。
* 第三个问题（架构设计）：针对模块职责、单一职责、耦合度或扩展性提问。
"""

def read_source_codes(file_paths):
    content = ""
    for path in file_paths:
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                content += f"\n--- File: {path} ---\n"
                content += f.read() + "\n"
        else:
            print(f"⚠️ 找不到文件 {path}")
    return content

output_dir = "Docs/TechPoints"
os.makedirs(output_dir, exist_ok=True)
TEMP_PROMPT_FILE = ".temp_prompt.txt" 

# 4. 开始操控 Claude 终端
for idx, (tech_point, file_paths) in enumerate(tech_mapping.items(), 1):
    safe_filename = tech_point.split('（')[0].replace(' ', '').replace('/', '_')
    file_name = os.path.join(output_dir, f"{idx:02d}_{safe_filename}.md")
    
    if os.path.exists(file_name):
        print(f"[{idx}/{len(tech_mapping)}] ⏭️ 文档已存在，跳过: {tech_point}")
        continue

    print(f"[{idx}/{len(tech_mapping)}] ⏳ 正在让 Claude 深度思考并生成: {tech_point} ...")
    real_code = read_source_codes(file_paths)
    
    final_prompt = USER_PROMPT_TEMPLATE.format(
        global_context=GLOBAL_CONTEXT, 
        tech_point=tech_point, 
        code_content=real_code
    )
    
    with open(TEMP_PROMPT_FILE, "w", encoding="utf-8") as f:
        f.write(final_prompt)
    
    try:
        cmd = f'claude -p "请读取 {TEMP_PROMPT_FILE} 的内容，严格遵循指令执行。"'
        result = subprocess.run(cmd, shell=True, capture_output=True, text=True, encoding="utf-8")
        
        if result.returncode == 0:
            raw_output = result.stdout.strip()
            
            # 使用正则提取 <document> 标签内的纯净文档
            match = re.search(r'<document>([\s\S]*?)</document>', raw_output)
            if match:
                final_md = match.group(1).strip()
            else:
                # 容错机制：如果 Claude 忘了写 document 标签，直接砍掉 thinking 部分
                final_md = raw_output.split("</thinking>")[-1].replace("<document>", "").strip()
                
            with open(file_name, "w", encoding="utf-8") as f:
                f.write(final_md)
            print(f"✅ 已保存：{file_name}\n")
        else:
            print(f"❌ 终端调用失败：\n{result.stderr}\n")
            
        time.sleep(3) 
        
    except Exception as e:
        print(f"❌ 程序报错：{e}\n")

if os.path.exists(TEMP_PROMPT_FILE):
    os.remove(TEMP_PROMPT_FILE)

print("🎉 《Operation Marigold》终极硬核面试文档全部打造完毕！")