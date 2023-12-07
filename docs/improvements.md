# Improvements

## General

- Move to `Octokit.WebHookEvents`

## Trie

- Use a mutable trie during construction
- Don't index stop words

## Queries

Reduce the size of the query objects and also simplify the way we construct
them. There are a lot of collections and copy and paste code. Maybe we can use a
representation that can be executed and also printed?
