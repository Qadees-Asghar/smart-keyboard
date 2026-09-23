"""
Builds the data files for SmartKeyboard:

    src/SmartKeyboard.App/Data/words.txt     "word count"
    src/SmartKeyboard.App/Data/bigrams.txt   "word1 word2 count"

Where the data comes from, all free to use:

1. count_1w.txt    333,000 words with real counts, from a very large web
                   corpus. Modern English, but it also carries web junk and
                   misspellings, so it is filtered below.
2. words_alpha     370,000 words that are actually in an English dictionary.
                   Used to throw the junk out of the list above.
3. count_2w.txt    286,000 word pairs with counts, for next word prediction.
4. Project Gutenberg books. Used for one thing only: neither web list keeps
   apostrophes, so without the books "don't" and "it's" would be missing and
   the spell checker would mark them as mistakes.

Run it with:  python tools/generate_dictionary.py
You only need to run it again if you want to rebuild the data files.
"""

import os
import re
import sys
import urllib.request
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "..", "src", "SmartKeyboard.App", "Data")
CACHE_DIR = os.path.join(HERE, ".cache")

WEB_WORDS = "https://norvig.com/ngrams/count_1w.txt"
WEB_PAIRS = "https://norvig.com/ngrams/count_2w.txt"
VALID_WORDS = "https://raw.githubusercontent.com/dwyl/english-words/master/words_alpha.txt"

# Wikipedia's list of common misspellings, written out for machines to read.
# It is a curated human list, which is worth more here than any rule, because
# the near neighbour rule below cannot be pushed any harder without eating
# real words. Measured: extending it to four letter words would have deleted
# "deft", "dour", "ewer", "fret", "hone", "laud", "pare", "sate", "wane" and
# "yore" to catch nine misspellings. This list catches a hundred and costs
# nothing.
WIKI_MISSPELLINGS = (
    "https://en.wikipedia.org/wiki/"
    "Wikipedia:Lists_of_common_misspellings/For_machines?action=raw"
)

BOOKS = {
    "pride_and_prejudice": "https://www.gutenberg.org/files/1342/1342-0.txt",
    "alice_in_wonderland": "https://www.gutenberg.org/files/11/11-0.txt",
    "sherlock_holmes": "https://www.gutenberg.org/files/1661/1661-0.txt",
    "frankenstein": "https://www.gutenberg.org/files/84/84-0.txt",
    "tale_of_two_cities": "https://www.gutenberg.org/files/98/98-0.txt",
    "dracula": "https://www.gutenberg.org/files/345/345-0.txt",
    "war_of_the_worlds": "https://www.gutenberg.org/files/36/36-0.txt",
    "moby_dick": "https://www.gutenberg.org/files/2701/2701-0.txt",
    "great_expectations": "https://www.gutenberg.org/files/1400/1400-0.txt",
    "treasure_island": "https://www.gutenberg.org/files/120/120-0.txt",
}

# The biggest the finished dictionary is allowed to get. The project aims at
# around 100,000 words, which is what the speed targets were written against.
MAX_WORDS = 100000

# Web counts run into the billions and will not fit the file format, so
# everything is scaled down until the commonest word lands near this number.
TOP_COUNT = 200000

MAX_WORD_LENGTH = 20

# A word is letters, and may hold apostrophes inside it (don't, it's).
WORD_RE = re.compile(r"[a-z]+(?:'[a-z]+)*")
SENTENCE_END_RE = re.compile(r"[.!?;:\n]")

# Chapter numbers like "iii", "vii" and "xxx". Only i, v and x are used on
# purpose: no English word is built from just those three letters, while
# adding c, d, l or m would wrongly catch real words like "did" and "mix".
ROMAN_RE = re.compile(r"[ivx]{2,}")

# Short words need their own rules. The web list is full of fragments like
# "ot", "ts", "nt" and "ll". Letting those in means a spell checker that never
# complains about a real typo, so for one and two letters we use a fixed list.
ONE_LETTER_WORDS = {"a", "i"}

TWO_LETTER_WORDS = {
    # real English two letter words
    "am", "an", "as", "at", "ax", "be", "by", "do", "go", "ha", "he", "hi",
    "id", "if", "in", "is", "it", "lo", "ma", "me", "my", "no", "of", "oh",
    "ok", "on", "or", "ox", "pa", "so", "to", "up", "us", "we", "ye",
    # everyday abbreviations people actually type
    "cd", "dr", "mr", "ms", "pm", "st", "tv", "uk",
}

# Contractions with the apostrophe missing. People type them that way online,
# so the web list is full of them, but they are misspellings and a spell
# checker must not accept them.
# Contractions typed without their apostrophe. Letting these in would make
# them real words, and the spell checker would then never put the mark back.
#
# "ill" is deliberately NOT here, even though it is how people type "I'll".
# It is also an ordinary English word, as in feeling ill, and leaving it out
# put a red underline under correct writing. The same call is made for "its"
# and "were": when a word is real in its own right it stays, and autocorrect
# leaves it alone, because there is no way to tell which was meant.
BROKEN_CONTRACTIONS = {
    "im", "ive", "youre", "youve", "youll", "hes", "shes", "weve",
    "theyre", "theyve", "theyll", "thats", "whats", "wheres", "whos",
    "dont", "doesnt", "didnt", "cant", "couldnt", "wouldnt", "shouldnt",
    "isnt", "arent", "wasnt", "werent", "hasnt", "havent", "hadnt", "wont",
    "aint", "heres", "theres", "youd", "theyd", "hed", "itd",
    # The left half of a contraction, which is not a word on its own.
    # "haven", "don" and "won" are left out of this: they are real words.
    "isn", "doesn", "didn", "wasn", "weren", "couldn", "wouldn", "shouldn",
    "hasn", "hadn", "aren", "shan", "mustn", "needn", "mightn", "oughtn",
}

# Dialect spellings from the old books that the web list also happens to
# contain, so neither filter catches them on its own.
DIALECT_JUNK = {"dat", "dem", "dey", "dis", "nuff", "yer", "wuz", "afore"}

# The English word list is a proper dictionary, so it does not know everyday
# computer words. Without these, typing "website" would be marked as a
# mistake. They still have to earn a count from the web list, so anything
# here that nobody actually types will not get in.
# Ordinary short words that are simply rare in web text. "wok", "oar" and
# "sob" all score below any floor worth setting, and they are still words
# people write, so they are named here and let through.
EVERYDAY_SHORT_WORDS = {
    "jog", "oar", "sob", "wag", "woe", "wok", "hoe", "pry", "sly", "elm",
    "eel", "ewe", "fig", "fir", "hue", "ivy", "keg", "lark", "moss", "nib",
    "oat", "pod", "pug", "ram", "rye", "sap", "sow", "tad", "vat", "yak",
}

MODERN_WORDS = {
    "website", "websites", "webpage", "webpages", "username", "usernames",
    "login", "logout", "signup", "online", "offline", "email", "emails",
    "blog", "blogs", "blogger", "podcast", "podcasts", "laptop", "laptops",
    "smartphone", "smartphones", "app", "apps", "wifi", "browser", "browsers",
    "url", "urls", "pdf", "jpeg", "png", "html", "css", "javascript",
    "dataset", "datasets", "database", "databases", "filename", "filenames",
    "checkbox", "dropdown", "hashtag", "hashtags", "emoji", "selfie",
    "google", "youtube", "facebook", "twitter", "whatsapp", "instagram",
    "linkedin", "github", "wikipedia", "android", "iphone", "windows",
    "microsoft", "netflix", "spotify", "chatbot", "internet", "intranet",
    "cybersecurity", "malware", "firewall", "login", "backup", "backups",
}


# Words people type to each other that barely appear in the web text these
# counts came from. Without them the program underlines "bro" and "wassup" in
# red, offers nothing when you start typing them, and would happily autocorrect
# them into something else.
#
# The counts are set by hand, on the same scale as the rest of the file, where
# "the" is 200000. They are deliberately modest: high enough to be suggested
# and to be left alone by the spell checker, low enough never to outrank
# ordinary English.
#
# Nothing shorter than three letters is here on purpose. Two letter additions
# turn up as a close match for almost every other short typo, and that costs
# more than it gives.
INFORMAL_WORDS = {
    "wassup": 60, "sup": 90, "hiya": 60, "yep": 320, "yup": 180, "nah": 320,
    "hmm": 200, "huh": 180, "ugh": 90, "yay": 110, "oops": 150,
    "bruh": 90, "bro": 500, "bros": 80, "fam": 60,
    "lol": 420, "lmao": 140, "rofl": 40, "omg": 260, "btw": 320, "fyi": 300,
    "idk": 140, "imo": 110, "imho": 40, "tbh": 110, "ikr": 40, "brb": 80,
    "pls": 160, "plz": 80, "thx": 90, "wtf": 190,
    "y'all": 190, "kinda": 300, "sorta": 110, "lemme": 90, "outta": 90,
    "cuz": 90, "coz": 50, "innit": 30,
    "selfie": 150, "selfies": 70, "emoji": 230, "emojis": 90,
    "meme": 260, "memes": 170, "vlog": 50, "vlogger": 20,
    "screenshot": 430, "screenshots": 180, "hashtag": 140, "hashtags": 60,
    "livestream": 60, "unfollow": 30, "retweet": 50, "repost": 40,
    "podcasts": 120, "chatbot": 180, "chatbots": 80, "unsubscribe": 80,
    "legit": 110, "vibe": 110, "vibes": 90, "cringe": 60, "sus": 40,
    "noob": 50,
}


# A rare word that is one typo away from a much more common word is almost
# certainly that typo. "enviroment" appears 3 times next to "environment" at
# 881, so it is a misspelling that crept into the word list, not a word.
#
# A plain count floor cannot do this job. Real but uncommon words like
# "quandary" and "meticulous" have counts just as low, and would be thrown out
# with the misspellings. The ratio to a near neighbour is what separates them.
MISSPELLING_MAX_COUNT = 25
MISSPELLING_RATIO = 50

# Three letter words are short enough that junk really hurts. A real three
# letter word is used constantly, so a tiny count means it is an abbreviation
# or something obscure that nobody typing English actually wants.
# How common a three letter word has to be to get in.
#
# This was 50, and it was cutting real English. Measured against the word
# list: of 252 everyday three letter words, a floor of 50 kept 201, and a
# floor of 10 keeps 246. "ill", "hen", "owl", "hug", "jaw", "pea" and forty
# others were being thrown away, so the spell checker underlined them.
#
# The cost is about six hundred more three letter entries, and roughly half
# of those are abbreviations nobody types. That is the better trade: junk
# sitting unused in the dictionary is invisible, while a missing real word
# puts a red line under correct writing and invites a wrong correction.
MIN_COUNT_FOR_THREE_LETTERS = 10


def decode(raw):
    """
    Turns downloaded bytes into text.

    Project Gutenberg files are a mix of UTF-8 and Windows-1252. Guessing
    wrong matters here: the curly apostrophe turns into rubbish, and then
    contractions split into fake words. So try UTF-8 strictly first.
    """
    for encoding in ("utf-8", "cp1252", "latin-1"):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", "replace")


def download(name, url):
    """Downloads a file once and keeps the raw bytes in tools/.cache."""
    os.makedirs(CACHE_DIR, exist_ok=True)
    path = os.path.join(CACHE_DIR, name + ".bin")

    if os.path.exists(path) and os.path.getsize(path) > 1000:
        with open(path, "rb") as handle:
            return decode(handle.read())

    print("  downloading " + name)
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    raw = urllib.request.urlopen(request, timeout=120).read()

    with open(path, "wb") as handle:
        handle.write(raw)

    return decode(raw)


def strip_gutenberg_wrapper(text):
    """Removes the Project Gutenberg licence header and footer."""
    start = re.search(r"\*\*\* ?START OF TH[EI]S? PROJECT GUTENBERG[^*]*\*\*\*", text)
    if start:
        text = text[start.end():]

    end = re.search(r"\*\*\* ?END OF TH[EI]S? PROJECT GUTENBERG[^*]*\*\*\*", text)
    if end:
        text = text[:end.start()]

    return text


def clean(text):
    """Lower cases everything and makes curly quotes plain."""
    return text.lower().replace("’", "'").replace("‘", "'")


def keep_word(word, count=None):
    """
    Decides whether a word is good enough for the dictionary.

    If junk gets in, real typos stop being flagged and the spell checker
    becomes useless, so this errs on the strict side for short words.
    """
    if len(word) > MAX_WORD_LENGTH or not word.isalpha():
        return False

    if len(word) == 1:
        return word in ONE_LETTER_WORDS

    if len(word) == 2:
        return word in TWO_LETTER_WORDS

    if word in BROKEN_CONTRACTIONS or word in DIALECT_JUNK:
        return False

    if ROMAN_RE.fullmatch(word):
        return False

    if (len(word) == 3
            and word not in MODERN_WORDS
            and word not in INFORMAL_WORDS
            and word not in EVERYDAY_SHORT_WORDS
            and count is not None
            and count < MIN_COUNT_FOR_THREE_LETTERS):
        return False

    return True


def load_known_misspellings():
    """Reads Wikipedia's list as {misspelling: the one correct spelling}."""
    found = {}

    for line in download("wiki_misspellings", WIKI_MISSPELLINGS).splitlines():
        if "->" not in line:
            continue

        bad, good = line.split("->", 1)
        bad = bad.strip().lower()
        corrections = [part.strip().lower() for part in good.split(",")]

        # Only single words, and only where there is one right answer. An
        # entry with two possible corrections cannot tell us anything.
        if len(corrections) != 1:
            continue
        if not WORD_RE.fullmatch(bad) or not WORD_RE.fullmatch(corrections[0]):
            continue

        found.setdefault(bad, corrections[0])

    return found


def drop_known_misspellings(words):
    """
    Removes words a human list says are misspellings.

    Three things all have to hold, so that a word is never dropped on the
    say so of the list alone:
      the right spelling is in our dictionary too, or we would be removing
        a word and offering nothing in its place,
      the misspelling is rare here, so anything people genuinely write
        survives, which is how "thru" stays, and
      the right spelling is the commoner of the two.
    """
    dropped = []

    for bad, good in load_known_misspellings().items():
        if bad not in words or good not in words:
            continue

        if words[bad] <= MISSPELLING_MAX_COUNT and words[good] > words[bad]:
            dropped.append(bad)

    for word in dropped:
        del words[word]

    return len(dropped)


def one_edit_away(word):
    """Every word that is one insert, delete, swap or change away from this one."""
    letters = "abcdefghijklmnopqrstuvwxyz"
    splits = [(word[:i], word[i:]) for i in range(len(word) + 1)]

    for left, right in splits:
        if right:
            yield left + right[1:]                      # delete a letter
        if len(right) > 1:
            yield left + right[1] + right[0] + right[2:]  # swap two letters
        for c in letters:
            if right:
                yield left + c + right[1:]              # change a letter
            yield left + c + right                      # insert a letter


def drop_misspellings(words):
    """
    Removes rare words that sit one typo away from a far more common word.

    Without this, a misspelling like "enviroment" counts as a real word, so
    the spell checker never marks it and autocorrect never fixes it.
    """
    dropped = []

    for word, count in list(words.items()):
        if count > MISSPELLING_MAX_COUNT or len(word) < 5:
            continue

        for neighbour in one_edit_away(word):
            if neighbour == word:
                continue

            other = words.get(neighbour)
            if other is not None and other >= count * MISSPELLING_RATIO:
                dropped.append(word)
                break

    for word in dropped:
        del words[word]

    return len(dropped)


def load_valid_words():
    """The words that are really in an English dictionary."""
    text = download("words_alpha", VALID_WORDS)
    return {w.strip().lower() for w in text.split() if w.strip()}


def load_web_counts():
    """Reads the "word<tab>count" lines from the web frequency list."""
    counts = {}

    for line in download("count_1w", WEB_WORDS).splitlines():
        parts = line.split("\t")
        if len(parts) != 2:
            continue

        try:
            counts[parts[0].strip().lower()] = int(parts[1])
        except ValueError:
            continue

    return counts


def load_web_pairs():
    """Reads the "word1 word2<tab>count" lines from the web pair list."""
    pairs = []

    for line in download("count_2w", WEB_PAIRS).splitlines():
        parts = line.split("\t")
        if len(parts) != 2:
            continue

        words = parts[0].strip().lower().split()
        if len(words) != 2:
            continue

        try:
            pairs.append((words[0], words[1], int(parts[1])))
        except ValueError:
            continue

    return pairs


def contractions_from_books():
    """
    Neither web list keeps apostrophes, so "don't" and "it's" would be missing
    and would be marked as mistakes. The books still have them.
    """
    text = []
    for name, url in BOOKS.items():
        text.append(clean(strip_gutenberg_wrapper(download(name, url))))

    words = Counter()
    for sentence in SENTENCE_END_RE.split("\n".join(text)):
        words.update(t for t in WORD_RE.findall(sentence) if len(t) <= MAX_WORD_LENGTH)

    return {w: c for w, c in words.items() if "'" in w and c >= 3}


def main():
    print("Reading the word lists")
    valid = load_valid_words()
    web = load_web_counts()
    print("  %d real English words, %d words with web counts" % (len(valid), len(web)))

    print("Keeping only real words, and only the commonest")

    # Scale first, so the short word rule can be written in the same numbers
    # that end up in the file.
    biggest = max(web.values()) if web else 1
    scale = TOP_COUNT / biggest

    kept = {}
    for word, raw in web.items():
        if (word not in valid
                and word not in MODERN_WORDS
                and word not in INFORMAL_WORDS):
            continue

        count = max(1, round(raw * scale))
        if keep_word(word, count):
            kept[word] = count

    ranked = sorted(kept.items(), key=lambda pair: -pair[1])[:MAX_WORDS]
    words = dict(ranked)
    print("  kept %d words" % len(words))

    print("Dropping misspellings that sit next to a much commoner word")
    removed = drop_misspellings(words)
    print("  dropped %d, %d words left" % (removed, len(words)))

    print("Dropping words a human list says are misspellings")
    removed = drop_known_misspellings(words)
    print("  dropped %d, %d words left" % (removed, len(words)))

    print("Adding the words people type to each other")
    for word, count in INFORMAL_WORDS.items():
        # Never lower a count the web text already justified.
        words[word] = max(words.get(word, 0), count)
    print("  %d in the list" % len(INFORMAL_WORDS))

    print("Adding the words with apostrophes")
    added = 0
    for word, count in contractions_from_books().items():
        if word not in words:
            words[word] = count
            added += 1
    print("  added %d" % added)

    print("Reading the word pairs")
    pairs = [
        row for row in load_web_pairs()
        if row[0] in words and row[1] in words
    ]

    if pairs:
        biggest_pair = max(count for _, _, count in pairs)
        pair_scale = TOP_COUNT / biggest_pair
        pairs = [(a, b, max(1, round(c * pair_scale))) for a, b, c in pairs]

    pairs.sort(key=lambda row: (-row[2], row[0], row[1]))
    print("  kept %d word pairs" % len(pairs))

    os.makedirs(OUT_DIR, exist_ok=True)
    words_path = os.path.join(OUT_DIR, "words.txt")
    bigrams_path = os.path.join(OUT_DIR, "bigrams.txt")

    with open(words_path, "w", encoding="utf-8", newline="\n") as handle:
        for word, count in sorted(words.items(), key=lambda pair: (-pair[1], pair[0])):
            handle.write("%s %d\n" % (word, count))

    with open(bigrams_path, "w", encoding="utf-8", newline="\n") as handle:
        for first, second, count in pairs:
            handle.write("%s %s %d\n" % (first, second, count))

    print("Wrote %d words to %s" % (len(words), os.path.normpath(words_path)))
    print("Wrote %d word pairs to %s" % (len(pairs), os.path.normpath(bigrams_path)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
