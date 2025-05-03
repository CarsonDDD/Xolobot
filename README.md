Discord DND bot for managing player inventories and handling shop interactions. The goal is to make the features easy to use and visually straight forward through discords primitive UI features, only using typed commands when necessary.

Old revitalized hackathon project.

Most the direct DB interaction is or started as AI-gen because I value the health of my fingers and couldnt be bothered to manually convert from mongo to sql.

Final todo before in a semi-polished usable state where I can abandon the project:
- [ ] add proper readme + db schema
- [ ] build instructions


# Demos

## Shop Interaction

### Shop open
![](shop-open.gif)

### Selecting an item
![](shop-select.gif)

### Skip the shop menu

It is slow to navigate the items in the shop one by one. Using this opens a dropdown for faster navigation. In the future the normal view will either be merged with this or removed entirely for this. 

![](direct-buy.gif)

## Player Data

Inventory can either be displayed as a basic text list or as the same view from the shop---giving the option to either sell the item, or delete the item (ex: you used a potion, so you must delete it from your inventory later.)

Inventories are private and can only been seen by the correct player; users with server admin can look and sell from any inventory.

![](inventory.gif)


![](profile.gif)


# DB Diagram
