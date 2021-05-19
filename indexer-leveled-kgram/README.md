# Why Inverted index:

In suggestion database, we can obtain that there are plenty of repeated words, saving its bigram (k-gram, as current implementation) may explodes

Solution description:

Let's denote:

```
suggestion: the shingles such as "black dress", "green top suite", and so on
suggestion id: 

token: a single word, ".", "," "-", for example: "black", "top", and so on
bigram: 2 consecutive characters in a token, e.g: "black" -> list of bigram: [$b, bl, la, ac, ck, k$]
```

## Indexes
We will have 2 indexes:

1. Inverted index ``token id > suggestion id``

Examples:

```
1. black dress
2. black top suite
3. green suite

tokens:

1. black
2. dress
3. top
4. suite
5. green

So inverted index are
1 (black) > 1, 2 
2 (dress) > 1
3 (top)   > 2
4 (suite) > 2, 3
5 (green) > 3
```

Pros:
+ Super fast index and saves spaces as tokens are different and number of unique tokens is small

2. Bigram - token index

For typo tolerance I suggest we use bigram index to recover mispelled words and give suggestions. 
The idea behind this is simpple: If a word is mispell (not found in dictionary), we resolve it by comparing bigrams to all matching bigrams and give top hit words, after the ward, use these top hit to search in inverted index.

## Performing crud

We keep tracking the suggestions and its reference to product id, build a forward index ``product id > suggestion id`` and ``suggestion > reference count``, ``token > reference count``

Once the suggestion is inserted, we tokenize it, then we want to increase suggestion reference count...
If we found suggestion newly created, we should check if tokens are new then we should create it in ``token > reference count``, otherwise just increase token reference count

On deletion, just revert the insertion work. Once the product id is deleted, look up for its suggestion ids, decrease suggestion references; aware that if suggestion references back to 0, it means that the suggestion is no longer exists, thus decrease the token reference, if token is referenced for 0 times, we delete it in the ``bigram - token`` index

A bit complicated but it should work, in the future we may not delete this, just let user pay for it.