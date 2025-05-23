# Envelopes 

Imagine this: you are a ruler, and you would like to discuss an alliance with a neighboring kingdom.
To start the discussions, you will have to contact the king. For this, you have 4 options: 
1) Visit the kingdom and discuss it in person. This takes time and is risky.
2) Invite the king over. This also takes time and planning, but probably is less risky.
3) Send a messenger with a verbal message to the king. Safe and fast, but requires you to have a trustworthy messenger. 
4) Send a letter to the king. Safe and fast, but requires you to have a trustworthy courier. 

You decide to take out your ink and quill, and draft a letter:

> Dear king, I would like to form an alliance between us. Hope to hear back from you soon.

You sign the letter and find a trusted courier. The courier takes the letter to the king. Later that week, you receive a reply and you successfully manage to establish the alliance. 

------- 

# 1st Iteration

In Vintage Story, there is currently no way to hide the contents of such a letter. The courier could actually have been working with some interested 3rd parties, and now your confidential information is out there. 

This problem could be alleviated by putting your letter inside an envelope and sealing it with a wax stamp. The courier might still break the seal and read its contents, but at least the receiving end would know that the envelope had been tampered with. This gives you a chance to go after such a courier. 

Such a solution is exactly what this mod aims to implement. 
Initially, the solution would be done in a simple way:
1) You can put a letter (any parchment) into an empty envelope by using them in the crafting grid. 
2) You can then seal the envelope by using a piece of beeswax on an envelope (in the crafting grid). This will put the envelope in a sealed state, and include the name of the sealer on the item. 
3) The envelope item itself would now only carry a reference ID of the contents, and the name of the sealer. Nothing else. 
4) You can use right click to unseal the envelope. For wax-sealed envelopes, a confirmation box will pop up.  The graphics will be updated to show a broken seal, and the item itself will also mention that it has been opened. You will receive the letter inside and at this point the contents of the letter would also be fetched from the server. You will be able to keep the unsealed envelope, should you wish to put the letter back inside (or another letter, for that matter). 

Future plans:
* in-world crafting of envelopes 
* more realistic wax by combining wax with resin and some coloring agent. 
* in-world sealing of envelopes, possibly by having to melt the wax beforehand
* custom seal stamps


-----

# 2nd iteration

The second iteration of the mod will focus on making the sealing process a bit more realistic. An item -- wax seal stamp -- will be introduced.
A wax seal stamp can have a custom design, with an editor akin to something like in the Coinage mod. A wax seal will have an unique ID, using which seals can be differentiated. A seal can also be named (example: "The Royal Eagle") for immersion. 
For now, the stamp will be required in the crafting recipe, and from it, attributes will be copied over to indicate sealing data on the envelope itself. 

------

# 3rd iteration 

The third iteration focuses on adding From/To fields to the envelope. This makes the work of the courier easier, as they can now deliver the letter to the correct recipient.

------

# 4th iteration

The fourth iteration focuses on adding more wax colors to work with. This will be done by adding recipes to combine the wax with a dye.