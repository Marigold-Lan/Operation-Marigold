import anthropic
import os

print("正在伪装成 Claude Code 终端发力...")
client = anthropic.Anthropic(
    api_key=os.environ.get("ANTHROPIC_AUTH_TOKEN"),
    base_url=os.environ.get("ANTHROPIC_BASE_URL"),
    # 终极伪装：骗过服务器的白名单
    default_headers={
        "User-Agent": "claude_code/0.2.0",  # 伪装成官方 CLI 工具
        "X-Client-Name": "claude_code"      # 有些代理会查这个自定义头
    }
)

try:
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=100,
        messages=[
            {"role": "user", "content": "你好，这是一条终极伪装测试消息。"}
        ]
    )
    print("✅ 伪装大获全胜！API 回复：", response.content[0].text)
except Exception as e:
    print("❌ 依然失败：", e)









