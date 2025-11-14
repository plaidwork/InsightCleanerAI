from pathlib import Path

path = Path('Services/CloudProxyInsightProvider.cs')
text = path.read_text(encoding='utf-8')
text = text.replace('var summary = "$"{reference.Title}??{reference.Content}";', 'var summary = "$"{reference.Title}：{reference.Content}";')

