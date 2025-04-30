Discord DND bot for managing player inventories and handling shop interactions. The goal is to make the features easy to use and visually straight forward through discords primitive UI features, only using typed commands when necessary.

Old revitalized hackathon project.

Most the direct DB interaction is or started as AI-gen because I value the health of my fingers and couldnt be bothered to manually convert from mongo to sql.

Final todo before in a semi-polished usable state where I can abandon the project:
- [x] show inv as simple list with quantity---used for knowledge when playing. not any asthetics, or shop view
- [ ] xolobot text/speaking messages
    - [ ] add messages (not AI just yet)
    - [ ] Better confirmation messages
- [ ] Markup/Markdown when buying/selling
- [ ] Find a way to deal with message clutter
    - [x] Edit/delete messages instead of sending new ones
- [x] Trash/remove item
    - [x] 2 step. have drop down to allow for amount.
- [ ] redo items inside db to be real
- [ ] Basic Admin features
    - [ ] Have some Admin state so we can make changes to the following.
        - [ ] A player can only sell their OWN items
        - [ ] A player can only open their OWN inventory
    - [ ] tbd
- [ ] add proper readme + db schema

![](WIP.gif)