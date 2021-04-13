with open("real-suggests.txt", "r", encoding="utf8") as f:
    suggestions = f.read().split("\n");

with open("id-suggests.txt", "w", encoding="utf8") as f:
    for i in range(len(suggestions)):
        f.write("%d %s\n" % (i // 10, suggestions[i]))
